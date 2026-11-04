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
        readonly Forms.ContextMenuStrip _menu;
        readonly Forms.ToolStripMenuItem _show, _gravity, _wander, _phrases, _autostart;
        SettingsWindow _settings;

        public Tray(Config cfg, Pet pet)
        {
            _cfg = cfg;
            _pet = pet;
            _menu = new Forms.ContextMenuStrip();
            _show = Check("Показать Clawd", () => _cfg.Show, v => { _cfg.Show = v; _pet.ConfigChanged(false); });
            _menu.Items.Add(_show);
            _menu.Items.Add("Позвать к курсору", null, (_, _) => _pet.Recenter());
            _menu.Items.Add("Покормить печенькой 🍪", null, (_, _) => _pet.Feed());
            _menu.Items.Add(new Forms.ToolStripSeparator());
            _gravity = Check("Гравитация", () => _cfg.Gravity, v => { _cfg.Gravity = v; _pet.ConfigChanged(false); });
            _wander = Check("Гуляет сам", () => _cfg.Wander, v => { _cfg.Wander = v; _pet.ConfigChanged(false); });
            _phrases = Check("Реплики", () => _cfg.Phrases, v => { _cfg.Phrases = v; _pet.ConfigChanged(false); });
            _menu.Items.Add(_gravity);
            _menu.Items.Add(_wander);
            _menu.Items.Add(_phrases);
            _menu.Items.Add(new Forms.ToolStripSeparator());
            _menu.Items.Add("Настройки…", null, (_, _) => OpenSettings());
            _menu.Items.Add("Что Clawd помнит", null, (_, _) =>
                MessageBox.Show(_pet.DescribeMemory(), "Память Clawd", MessageBoxButton.OK, MessageBoxImage.Information));
            _autostart = Check("Запускать вместе с Windows", () => Config.Autostart, v => Config.Autostart = v);
            _menu.Items.Add(_autostart);
            _menu.Items.Add(new Forms.ToolStripSeparator());
            _menu.Items.Add("Выход", null, (_, _) => Application.Current.Shutdown());
            _menu.Opening += (_, _) => Sync();

            _icon = new Forms.NotifyIcon
            {
                Text = "Clawd",
                Icon = _pet.MakeTrayIcon(),
                ContextMenuStrip = _menu,
                Visible = true,
            };
            _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) _pet.Recenter(); };
        }

        Forms.ToolStripMenuItem Check(string text, Func<bool> get, Action<bool> set)
        {
            var item = new Forms.ToolStripMenuItem(text) { Checked = get(), Tag = get };
            item.Click += (_, _) => { set(!get()); item.Checked = get(); };
            return item;
        }

        void Sync()
        {
            foreach (var item in new[] { _show, _gravity, _wander, _phrases, _autostart })
                if (item.Tag is Func<bool> get) item.Checked = get();
        }

        public void ShowMenuAtCursor()
        {
            Sync();
            // NotifyIcon сам выводит приложение на передний план, иначе меню не закрывается по клику мимо
            var show = typeof(Forms.NotifyIcon).GetMethod("ShowContextMenu",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (show != null) show.Invoke(_icon, null);
            else _menu.Show(Forms.Cursor.Position);
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
            _menu.Dispose();
        }
    }
}
