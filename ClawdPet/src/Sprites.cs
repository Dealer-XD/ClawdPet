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

        static readonly (uint Bg, uint Glyph, string[] Rows)[] FallbackIcons =
        {
            (0xFF2E6FD9, 0xFFFFFFFF, new[] { ".XXXXX.", "XX...XX", "X..X..X", "X.XXX.X", "X..X..X", "XX...XX", ".XXXXX." }),
            (0xFFD9A55B, 0xFF5A3A1E, new[] { "..XXX..", ".X...X.", "XXXXXXX", "X.....X", "X.....X", "X.....X", "XXXXXXX" }),
            (0xFF26241F, 0xFF5FD98A, new[] { "XXXXXXX", "X.....X", "X.X...X", "X..X..X", "X.X...X", "X.....X", "XXXXXXX" }),
            (0xFF7B61FF, 0xFFFFFFFF, new[] { "..XXX..", ".X...X.", "X.....X", "X.....X", "X.....X", ".X...X.", "..XXX.." }),
        };

        static bool InsideRound(int x, int y, int w, int h, int r)
        {
            int nx = x < r ? r - x : (x >= w - r ? x - (w - r - 1) : 0);
            int ny = y < r ? r - y : (y >= h - r ? y - (h - r - 1) : 0);
            return nx * nx + ny * ny <= r * r;
        }

        static int Blend(int dst, int src)
        {
            double a = ((src >> 24) & 0xFF) / 255.0;
            int r = (int)(((src >> 16) & 0xFF) * a + ((dst >> 16) & 0xFF) * (1 - a));
            int g = (int)(((src >> 8) & 0xFF) * a + ((dst >> 8) & 0xFF) * (1 - a));
            int b = (int)((src & 0xFF) * a + (dst & 0xFF) * (1 - a));
            return unchecked((int)(0xFF000000u | (uint)(r << 16) | (uint)(g << 8) | (uint)b));
        }

        static void TileBackground(PixelCanvas cv, int size, uint fill)
        {
            int radius = Math.Max(3, size / 5);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (!InsideRound(x, y, size, size, radius)) continue;
                    bool inner = x > 0 && y > 0 && x < size - 1 && y < size - 1
                                 && InsideRound(x - 1, y - 1, size - 2, size - 2, Math.Max(1, radius - 1));
                    cv.Px[y * size + x] = unchecked((int)(inner ? fill : 0xFF4A463E));
                }
        }

        /// <summary>Настоящая иконка программы на тёмной «кнопке» панели задач.</summary>
        public static BitmapSource IconTile(BitmapSource icon, int size)
        {
            var cv = new PixelCanvas(size, size);
            TileBackground(cv, size, 0xFF26241F);
            if (icon == null) return cv.ToBitmap();
            BitmapSource src = icon.Format == PixelFormats.Bgra32
                ? icon
                : new FormatConvertedBitmap(icon, PixelFormats.Bgra32, null, 0);
            int sw = src.PixelWidth, sh = src.PixelHeight;
            if (sw <= 0 || sh <= 0) return cv.ToBitmap();
            int[] sp = new int[sw * sh];
            src.CopyPixels(sp, sw * 4, 0);
            int pad = Math.Max(2, size / 8);
            int inner = Math.Max(1, size - pad * 2);
            int ox = (size - inner) / 2, oy = (size - inner) / 2;
            for (int y = 0; y < inner; y++)
                for (int x = 0; x < inner; x++)
                {
                    int sx = x * sw / inner, sy = y * sh / inner;
                    int c = sp[sy * sw + sx];
                    if (((c >> 24) & 0xFF) < 8) continue;
                    int dx = ox + x, dy = oy + y;
                    if (!InsideRound(dx, dy, size, size, Math.Max(3, size / 5))) continue;
                    cv.Px[dy * size + dx] = Blend(cv.Px[dy * size + dx], c);
                }
            return cv.ToBitmap();
        }

        /// <summary>Запасная нарисованная иконка, если запущенных программ не нашлось.</summary>
        public static BitmapSource FallbackIcon(int size, int index)
        {
            var (bg, glyph, rows) = FallbackIcons[Math.Abs(index) % FallbackIcons.Length];
            var cv = new PixelCanvas(size, size);
            TileBackground(cv, size, bg);
            int pad = Math.Max(2, size / 8);
            int cell = Math.Max(1, (size - pad * 2) / rows.Length);
            int gw = rows[0].Length * cell, gh = rows.Length * cell;
            int ox = (size - gw) / 2, oy = (size - gh) / 2;
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < rows[y].Length; x++)
                    if (rows[y][x] == 'X')
                        cv.Rect(ox + x * cell, oy + y * cell, ox + (x + 1) * cell, oy + (y + 1) * cell, glyph);
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
