using System;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ClawdPet
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            using var mutex = new Mutex(true, "ClawdPet_SingleInstance_v2", out bool first);
            if (!first)
            {
                MessageBox.Show("Clawd уже живёт у тебя в трее ♥", "Clawd", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Native.timeBeginPeriod(1);
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.DispatcherUnhandledException += (_, e) =>
            {
                Log.Write("unhandled: " + e.Exception);
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("fatal: " + e.ExceptionObject);

            var cfg = Config.Load();
            var pet = new Pet(cfg);
            var tray = new Tray(cfg, pet);
            pet.OpenMenu = tray.ShowMenuAtCursor;
            pet.PaletteChanged = tray.RefreshIcon;
            app.Startup += (_, _) =>
            {
                if (cfg.Show) pet.Start(true);
                else tray.Balloon("Clawd спрятан. Нажми на иконку в трее, чтобы позвать.");
            };
            app.Exit += (_, _) =>
            {
                pet.Shutdown();
                tray.Dispose();
                Native.timeEndPeriod(1);
            };
            app.Run();
        }
    }

    public class Tray : IDisposable
    {
        readonly Config _cfg;
        readonly Pet _pet;
        readonly Forms.NotifyIcon _icon;
        SettingsWindow _settings;

        public Tray(Config cfg, Pet pet)
        {
            _cfg = cfg;
            _pet = pet;
            _icon = new Forms.NotifyIcon
            {
                Text = "Clawd",
                Icon = _pet.MakeTrayIcon(),
                Visible = true,
            };
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left) _pet.Recenter();
                else if (e.Button == Forms.MouseButtons.Right) ShowMenuAtCursor();
            };
        }

        public void ShowMenuAtCursor()
        {
            var menu = new ClaudeMenu(_cfg, _pet, OpenSettings);
            menu.IsOpen = true;
        }

        public void OpenSettings()
        {
            if (_settings != null) { _settings.Activate(); return; }
            _settings = new SettingsWindow(_cfg, _pet);
            _settings.Closed += (_, _) => _settings = null;
            _settings.Show();
            _settings.Activate();
        }

        public void RefreshIcon()
        {
            var old = _icon.Icon;
            _icon.Icon = _pet.MakeTrayIcon();
            old?.Dispose();
        }

        public void Balloon(string text) => _icon.ShowBalloonTip(4000, "Clawd", text, Forms.ToolTipIcon.Info);

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
    }
}
