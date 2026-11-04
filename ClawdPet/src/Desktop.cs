using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClawdPet
{
    /// <summary>Видимый кусок верхнего края окна, на котором можно стоять.</summary>
    public class Platform
    {
        public IntPtr Hwnd;
        public double X0, X1, Top, Left;
        public string Name;
    }

    public static class Desktop
    {
        static readonly HashSet<string> SkipClasses = new()
        {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
            "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland",
            "ForegroundStaging", "MultitaskingViewFrame", "TaskListThumbnailWnd", "#32768",
        };

        struct Win { public IntPtr H; public Native.RECT R; }

        /// <summary>Видимые окна в Z-порядке сверху вниз.</summary>
        static List<Win> Windows(int selfPid)
        {
            var list = new List<Win>();
            Native.EnumWindows((h, _) =>
            {
                if (!Native.IsWindowVisible(h) || Native.IsIconic(h) || Native.Cloaked(h)) return true;
                Native.GetWindowThreadProcessId(h, out uint pid);
                if (pid == selfPid) return true;
                long ex = Native.GetLong(h, Native.GWL_EXSTYLE);
                if ((ex & Native.WS_EX_LAYERED) != 0 && (ex & Native.WS_EX_TRANSPARENT) != 0) return true;
                if ((ex & Native.WS_EX_TOOLWINDOW) != 0 && (ex & Native.WS_EX_APPWINDOW) == 0) return true;
                if (SkipClasses.Contains(Native.ClassName(h))) return true;
                var r = Native.Bounds(h);
                if (r.Right - r.Left < 120 || r.Bottom - r.Top < 60) return true;
                list.Add(new Win { H = h, R = r });
                return true;
            }, IntPtr.Zero);
            return list;
        }

        /// <summary>Сканирует видимые (не перекрытые сверху) участки верхних краёв окон.</summary>
        static List<Platform> ScanAll(int selfPid)
        {
            var result = new List<Platform>();
            var wins = Windows(selfPid);
            for (int i = 0; i < wins.Count; i++)
            {
                var r = wins[i].R;
                var segs = new List<(double, double)> { (r.Left, r.Right) };
                for (int j = 0; j < i && segs.Count > 0; j++)
                {
                    var o = wins[j].R;
                    if (!(o.Top <= r.Top + 1 && r.Top + 1 < o.Bottom)) continue;
                    var next = new List<(double, double)>();
                    foreach (var (a, b) in segs)
                    {
                        if (o.Right <= a || o.Left >= b) { next.Add((a, b)); continue; }
                        if (o.Left > a) next.Add((a, o.Left));
                        if (o.Right < b) next.Add((o.Right, b));
                    }
                    segs = next;
                }
                foreach (var (a, b) in segs)
                    if (b - a >= 24)
                        result.Add(new Platform { Hwnd = wins[i].H, X0 = a, X1 = b, Top = r.Top, Left = r.Left });
            }
            return result;
        }

        // ---------- кеш платформ ----------
        // Перечисление окон занимает единицы миллисекунд и раньше шло на UI-потоке
        // по несколько раз в секунду — из-за этого проседал FPS. Теперь скан идёт в фоне,
        // а на UI-потоке остаётся только фильтрация готового снимка.

        static readonly object PlatformLock = new();
        static List<Platform> _platforms = new();
        static long _platformsAt = long.MinValue / 2;
        static int _platformsBusy;
        static volatile bool _platformsInit;
        const int PlatformTtlMs = 220;

        static void Publish(List<Platform> snapshot)
        {
            lock (PlatformLock) _platforms = snapshot;
            Interlocked.Exchange(ref _platformsAt, Environment.TickCount64);
            _platformsInit = true;
        }

        public static List<Platform> ScanPlatforms(int selfPid, double minTop, double maxTop)
        {
            long now = Environment.TickCount64;
            bool stale = now - Interlocked.Read(ref _platformsAt) > PlatformTtlMs;
            if ((stale || !_platformsInit) && Interlocked.CompareExchange(ref _platformsBusy, 1, 0) == 0)
            {
                if (!_platformsInit)
                {
                    // Первый скан — сразу, чтобы питомец при запуске знал, где стоять.
                    try { Publish(ScanAll(selfPid)); }
                    catch (Exception e) { Log.Write("scan: " + e.Message); }
                    finally { Interlocked.Exchange(ref _platformsBusy, 0); }
                }
                else
                {
                    int pid = selfPid;
                    Task.Run(() =>
                    {
                        try { Publish(ScanAll(pid)); }
                        catch (Exception e) { Log.Write("scan: " + e.Message); }
                        finally { Interlocked.Exchange(ref _platformsBusy, 0); }
                    });
                }
            }
            List<Platform> snapshot;
            lock (PlatformLock) snapshot = _platforms;
            var result = new List<Platform>(snapshot.Count);
            foreach (var p in snapshot)
            {
                if (p.Top < minTop || p.Top > maxTop) continue;
                result.Add(new Platform { Hwnd = p.Hwnd, X0 = p.X0, X1 = p.X1, Top = p.Top, Left = p.Left, Name = p.Name });
            }
            return result;
        }

        static readonly Dictionary<uint, (string Key, string Name)> ProcCache = new();

        /// <summary>Запущенные программы, у которых есть окно на панели задач: имя и путь к exe.</summary>
        public static List<(string Name, string Exe)> VisibleApps(int selfPid)
        {
            var result = new List<(string Name, string Exe)>();
            List<Win> wins;
            try { wins = Windows(selfPid); }
            catch { return result; }
            foreach (var w in wins)
            {
                Native.GetWindowThreadProcessId(w.H, out uint pid);
                if (pid == 0 || pid == selfPid) continue;
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    string exe;
                    try { exe = p.MainModule?.FileName; } catch { continue; }
                    if (string.IsNullOrWhiteSpace(exe)) continue;
                    string name = p.ProcessName;
                    try
                    {
                        var desc = p.MainModule?.FileVersionInfo.FileDescription;
                        if (!string.IsNullOrWhiteSpace(desc) && desc.Length <= 40) name = desc.Trim();
                    }
                    catch { }
                    result.Add((name, exe));
                }
                catch { }
            }
            return result;
        }

        /// <summary>Активная программа: ключ (имя exe), понятное имя и заголовок окна.</summary>
        public static (string Key, string Name, string Title) Foreground(int selfPid)
        {
            var h = Native.GetForegroundWindow();
            if (h == IntPtr.Zero || h == Native.GetShellWindow()) return (null, null, null);
            if (SkipClasses.Contains(Native.ClassName(h))) return (null, null, null);
            Native.GetWindowThreadProcessId(h, out uint pid);
            if (pid == 0 || pid == selfPid) return (null, null, null);
            if (!ProcCache.TryGetValue(pid, out var info))
            {
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    string key = p.ProcessName.ToLowerInvariant();
                    string name = p.ProcessName;
                    try
                    {
                        var desc = p.MainModule?.FileVersionInfo.FileDescription;
                        if (!string.IsNullOrWhiteSpace(desc) && desc.Length <= 40) name = desc.Trim();
                    }
                    catch { }
                    info = (key, name);
                }
                catch { info = (null, null); }
                if (ProcCache.Count > 300) ProcCache.Clear();
                ProcCache[pid] = info;
            }
            if (info.Key == "explorer" && Native.ClassName(h) != "CabinetWClass") return (null, null, null);
            return (info.Key, info.Name, Native.Title(h));
        }

        public static string AppName(IntPtr h)
        {
            try
            {
                Native.GetWindowThreadProcessId(h, out uint pid);
                if (!ProcCache.TryGetValue(pid, out var info))
                {
                    using var p = Process.GetProcessById((int)pid);
                    string name = p.ProcessName;
                    try
                    {
                        var desc = p.MainModule?.FileVersionInfo.FileDescription;
                        if (!string.IsNullOrWhiteSpace(desc) && desc.Length <= 40) name = desc.Trim();
                    }
                    catch { }
                    info = (p.ProcessName.ToLowerInvariant(), name);
                    ProcCache[pid] = info;
                }
                return info.Name;
            }
            catch { return null; }
        }

        /// <summary>Окно на переднем плане развёрнуто на весь монитор (игра, видео, презентация).</summary>
        public static bool ForegroundIsFullscreen(int selfPid)
        {
            var h = Native.GetForegroundWindow();
            if (h == IntPtr.Zero || h == Native.GetShellWindow()) return false;
            var cls = Native.ClassName(h);
            if (SkipClasses.Contains(cls)) return false;
            Native.GetWindowThreadProcessId(h, out uint pid);
            if (pid == selfPid) return false;
            if (Native.IsZoomed(h) && (Native.GetLong(h, Native.GWL_STYLE) & Native.WS_CAPTION) == Native.WS_CAPTION) return false;
            Native.GetWindowRect(h, out var r);
            var screen = System.Windows.Forms.Screen.FromHandle(h).Bounds;
            return r.Left <= screen.Left && r.Top <= screen.Top && r.Right >= screen.Right && r.Bottom >= screen.Bottom;
        }
    }
}
