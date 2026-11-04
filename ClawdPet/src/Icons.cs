using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ClawdPet
{
    /// <summary>Иконка запущенной программы — то же, что видно на панели задач Windows.</summary>
    public sealed class AppIcon
    {
        public string Name;
        public BitmapSource Bmp;
    }

    /// <summary>
    /// Собирает настоящие иконки запущенных программ (через exe-файл процесса),
    /// чтобы Clawd мог «стащить» иконку с панели задач.
    /// </summary>
    public static class IconBank
    {
        static readonly object Lock = new();
        static readonly Random Rng = new();
        static List<AppIcon> _icons = new();
        static int _busy;
        static long _last;

        public static bool Ready
        {
            get { lock (Lock) return _icons.Count > 0; }
        }

        /// <summary>Обновляет набор иконок в фоне. Не чаще раза в 20 секунд.</summary>
        public static void Refresh(int selfPid, bool force = false)
        {
            long now = Environment.TickCount64;
            if (!force && now - Interlocked.Read(ref _last) < 20_000) return;
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            Interlocked.Exchange(ref _last, now);
            Task.Run(() =>
            {
                try
                {
                    var list = new List<AppIcon>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (name, exe) in Desktop.VisibleApps(selfPid))
                    {
                        if (!seen.Add(exe)) continue;
                        var bmp = Load(exe);
                        if (bmp == null) continue;
                        list.Add(new AppIcon { Name = name, Bmp = bmp });
                        if (list.Count >= 12) break;
                    }
                    if (list.Count > 0)
                    {
                        lock (Lock) _icons = list;
                    }
                }
                catch (Exception e) { Log.Write("icons: " + e.Message); }
                finally { Interlocked.Exchange(ref _busy, 0); }
            });
        }

        static BitmapSource Load(string exe)
        {
            try
            {
                using var ico = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (ico == null) return null;
                var bmp = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(32, 32));
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        /// <summary>Случайная «иконка с панели задач» или null, если собрать не удалось.</summary>
        public static AppIcon Take()
        {
            lock (Lock)
            {
                if (_icons.Count == 0) return null;
                return _icons[Rng.Next(_icons.Count)];
            }
        }
    }
}
