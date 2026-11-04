using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClawdPet
{
    public readonly record struct FrameKey(string Eyes, int Look, string Arms, string Legs, int Squash, bool Blush, string Mouth);

    /// <summary>Простой ARGB-холст: рисуем прямоугольниками-«пикселями», как в оригинале.</summary>
    public class PixelCanvas
    {
        public readonly int W, H;
        public readonly int[] Px;
        public PixelCanvas(int w, int h) { W = w; H = h; Px = new int[w * h]; }

        public void Rect(int x0, int y0, int x1, int y1, uint color)
        {
            x0 = Math.Max(0, x0); y0 = Math.Max(0, y0);
            x1 = Math.Min(W, x1); y1 = Math.Min(H, y1);
            int c = unchecked((int)color);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    Px[y * W + x] = c;
        }

        public BitmapSource ToBitmap()
        {
            var bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, Px, W * 4);
            bmp.Freeze();
            return bmp;
        }
    }

    public static class Sprites
    {
        public static uint Mix(uint a, uint b, double t)
        {
            uint o = 0xFF000000;
            foreach (int shift in new[] { 16, 8, 0 })
            {
                int ca = (int)((a >> shift) & 0xFF), cb = (int)((b >> shift) & 0xFF);
                o |= (uint)(int)(ca + (cb - ca) * t) << shift;
            }
            return o;
        }

        public static PixelCanvas RenderFrame(FrameKey key, int s, uint body, uint eye)
        {
            uint blushC = Mix(body, 0xFFFF4F79, 0.5);
            var cv = new PixelCanvas(C.GridW * s, C.GridH * s);
            void P(int x0, int y0, int x1, int y1, uint color) => cv.Rect(x0 * s, y0 * s, x1 * s, y1 * s, color);

            int left = 3, right = 15, top = 3, bottom = 10;
            if (key.Squash > 0) { left = 2; right = 16; top = 5; }
            else if (key.Squash < 0) { left = 4; right = 14; top = 2; }
            if (key.Legs == "tuck") { top += 1; bottom += 1; }

            int[] heights = key.Legs switch
            {
                "walk1" => new[] { 1, 2, 1, 2 },
                "walk2" => new[] { 2, 1, 2, 1 },
                "dangle" => new[] { 3, 3, 3, 3 },
                "tuck" => new[] { 1, 1, 1, 1 },
                _ => new[] { 2, 2, 2, 2 },
            };
            int[] cols = { left + 1, left + 3, right - 4, right - 2 };
            for (int i = 0; i < 4; i++) P(cols[i], bottom, cols[i] + 1, bottom + heights[i], body);

            void Arm(int side, string style)
            {
                if (style == "up")
                {
                    if (side < 0) { P(left - 2, top - 1, left - 1, top + 3, body); P(left - 1, top + 2, left, top + 3, body); }
                    else { P(right + 1, top - 1, right + 2, top + 3, body); P(right, top + 2, right + 1, top + 3, body); }
                }
                else if (style == "tuck")
                {
                    if (side < 0) P(left - 1, top + 3, left, top + 5, body);
                    else P(right, top + 3, right + 1, top + 5, body);
                }
                else
                {
                    if (side < 0) P(left - 2, top + 3, left, top + 5, body);
                    else P(right, top + 3, right + 2, top + 5, body);
                }
            }

            string la = key.Arms, ra = key.Arms;
            if (key.Arms == "wave1") { la = "up"; ra = "normal"; }
            else if (key.Arms == "wave2") { la = "normal"; ra = "up"; }
            Arm(-1, la);
            Arm(1, ra);

            P(left, top, right, bottom, body);

            int ey = top + 2;
            foreach (var (side, cx) in new[] { (-1, left + 3 + key.Look), (1, right - 4 + key.Look) })
            {
                switch (key.Eyes)
                {
                    case "blink": P(cx, ey + 1, cx + 1, ey + 2, eye); break;
                    case "happy":
                        P(cx - 1, ey + 1, cx, ey + 2, eye); P(cx, ey, cx + 1, ey + 1, eye); P(cx + 1, ey + 1, cx + 2, ey + 2, eye);
                        break;
                    case "surprised": P(cx, ey - 1, cx + 1, ey + 2, eye); break;
                    case "dizzy":
                        foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (0, 0), (-1, 1), (1, 1) })
                            P(cx + dx, ey + dy, cx + dx + 1, ey + dy + 1, eye);
                        break;
                    case "sleep": P(cx - 1, ey + 1, cx + 2, ey + 2, eye); break;
                    case "angry":
                    {
                        P(cx, ey, cx + 1, ey + 2, eye);
                        int brow = side < 0 ? cx - 1 : cx + 1;
                        P(brow, ey - 1, brow + 1, ey, eye);
                        break;
                    }
                    case "sad":
                    {
                        P(cx, ey, cx + 1, ey + 2, eye);
                        int brow = side < 0 ? cx + 1 : cx - 1;
                        P(brow, ey - 1, brow + 1, ey, eye);
                        break;
                    }
                    default: P(cx, ey, cx + 1, ey + 2, eye); break;
                }
                if (key.Blush)
                {
                    if (side < 0) P(cx - 2, ey + 2, cx, ey + 3, blushC);
                    else P(cx + 1, ey + 2, cx + 3, ey + 3, blushC);
                }
            }

            if (key.Mouth != null)
            {
                int mx = (left + right) / 2, my = ey + 2;
                if (key.Mouth == "open") P(mx - 1, my, mx + 1, Math.Max(my + 1, Math.Min(my + 2, bottom)), eye);
                else P(mx - 1, my, mx + 1, my + 1, eye);
            }
            return cv;
        }

        public static BitmapSource Pattern(string kind, int s)
        {
            var rows = C.Patterns[kind];
            Dictionary<char, uint> colors = kind switch
            {
                "poop" => C.PoopColors,
                "cookie" => C.CookieColors,
                "heart" => new() { ['X'] = C.HeartColor },
                "zzz" => new() { ['X'] = C.ZzzColor },
                "spark" => new() { ['X'] = C.SparkColor },
                _ => new() { ['X'] = C.NoteColor },
            };
            var cv = new PixelCanvas(rows[0].Length * s, rows.Length * s);
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (colors.TryGetValue(rows[y][x], out var c))
                        cv.Rect(x * s, y * s, (x + 1) * s, (y + 1) * s, c);
            return cv.ToBitmap();
        }

        /// <summary>Иконка для трея из того же спрайта.</summary>
        public static System.Drawing.Icon TrayIcon(uint body, uint eye)
        {
            var cv = RenderFrame(new FrameKey("open", 0, "normal", "stand", 0, false, null), 2, body, eye);
            int size = Math.Max(cv.W, cv.H);
            using var bmp = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            int ox = (size - cv.W) / 2, oy = (size - cv.H) / 2;
            for (int y = 0; y < cv.H; y++)
                for (int x = 0; x < cv.W; x++)
                {
                    int c = cv.Px[y * cv.W + x];
                    if (c != 0) bmp.SetPixel(x + ox, y + oy, System.Drawing.Color.FromArgb(c));
                }
            IntPtr h = bmp.GetHicon();
            var icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(h).Clone();
            Native.DestroyIcon(h);
            return icon;
        }
    }
}
