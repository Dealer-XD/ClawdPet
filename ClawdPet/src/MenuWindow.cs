using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ClawdPet
{
    /// <summary>Меню Clawd в виде терминала Claude Code: тёмная панель, оранжевый пунктир, моноширинный шрифт.</summary>
    public class ClaudeMenu : ContextMenu
    {
        readonly Config _cfg;
        readonly Pet _pet;
        readonly Action _openSettings;

        public ClaudeMenu(Config cfg, Pet pet, Action openSettings)
        {
            _cfg = cfg;
            _pet = pet;
            _openSettings = openSettings;
            Resources = Theme.Load();
            FontFamily = Theme.Font;
            FontSize = 12.5;
            SnapsToDevicePixels = true;
            Placement = PlacementMode.MousePoint;
            HasDropShadow = false;
            Items.Add(new MenuItem { Header = Build(), Style = (Style)FindResource("RawMenuItem") });
        }

        UIElement Build()
        {
            var root = new Grid { Width = 560 };
            root.Children.Add(new Border { Background = Theme.BgBrush, CornerRadius = new CornerRadius(6), Opacity = 0.985 });
            root.Children.Add(new Rectangle
            {
                Stroke = Theme.AccentBrush, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 }, RadiusX = 6, RadiusY = 6,
            });
            root.Children.Add(new TextBlock
            {
                Text = " Claude Code v2.0.0 ",
                Foreground = Theme.AccentBrush,
                Background = Theme.BgBrush,
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 12,
            });

            var body = new StackPanel { Margin = new Thickness(18, 30, 18, 14) };
            body.Children.Add(Header());
            body.Children.Add(Separator(12));

            body.Children.Add(ActionRow("Позвать к курсору", () => _pet.Recenter()));
            body.Children.Add(ActionRow("Съесть иконку с панели задач", () => _pet.Feed()));
            body.Children.Add(Separator(6));
            body.Children.Add(ToggleRow("Показать Clawd", () => _cfg.Show, v => { _cfg.Show = v; _pet.ConfigChanged(false); }));
            body.Children.Add(ToggleRow("Гравитация", () => _cfg.Gravity, v => { _cfg.Gravity = v; _pet.ConfigChanged(false); }));
            body.Children.Add(ToggleRow("Гуляет сам", () => _cfg.Wander, v => { _cfg.Wander = v; _pet.ConfigChanged(false); }));
            body.Children.Add(ToggleRow("Реплики", () => _cfg.Phrases, v => { _cfg.Phrases = v; _pet.ConfigChanged(false); }));
            body.Children.Add(ToggleRow("Таскает иконки сам", () => _cfg.EatIcons, v => { _cfg.EatIcons = v; _pet.ConfigChanged(false); }));
            body.Children.Add(ToggleRow("Запускать с Windows", () => Config.Autostart, v => Config.Autostart = v));
            body.Children.Add(Separator(6));
            body.Children.Add(ActionRow("Настройки…", () => _openSettings()));
            body.Children.Add(ActionRow("Что Clawd помнит", ShowMemory));
            body.Children.Add(ActionRow("Выход", () => Application.Current.Shutdown()));
            body.Children.Add(new TextBlock
            {
                Text = "esc — закрыть  ·  /help — настройки",
                Foreground = Theme.DimBrush,
                FontSize = 11.5,
                Margin = new Thickness(8, 10, 0, 0),
            });

            root.Children.Add(body);
            return root;
        }

        void ShowMemory()
        {
            ClaudeDialog.Info("Память Clawd", _pet.DescribeMemory());
        }

        FrameworkElement Header()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.16, GridUnitType.Star) });

            var welcome = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
            string name = _pet.DisplayName();
            welcome.Children.Add(new TextBlock
            {
                Text = name == null ? "Welcome back!" : $"Welcome back, {name}!",
                FontSize = 13.5,
                TextWrapping = TextWrapping.Wrap,
            });
            var portrait = new Image
            {
                Source = _pet.Portrait(6),
                Width = C.GridW * 6,
                Height = C.GridH * 6,
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 8),
            };
            RenderOptions.SetBitmapScalingMode(portrait, BitmapScalingMode.NearestNeighbor);
            welcome.Children.Add(portrait);
            welcome.Children.Add(new TextBlock
            {
                Text = $"Clawd v2.0.0 · {_pet.BondLabel()}",
                Foreground = Theme.DimBrush,
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            welcome.Children.Add(new TextBlock
            {
                Text = $"{_cfg.EatenCount} {Brain.Plural(_cfg.EatenCount, "иконка", "иконки", "иконок")} · " +
                       $"{_cfg.PetsCount} поглаживаний",
                Foreground = Theme.DimBrush,
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            var left = Box(welcome);
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            var recent = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
            recent.Children.Add(new TextBlock
            {
                Text = "Recent activity",
                Foreground = Theme.AccentBrush,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6),
            });
            var items = _pet.RecentActivity(4);
            if (items.Count == 0) items.Add(("", "пока ничего не было"));
            foreach (var (when, what) in items)
            {
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                var time = new TextBlock { Text = when, Foreground = Theme.DimBrush, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
                var text = new TextBlock { Text = what, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(text, 1);
                row.Children.Add(time);
                row.Children.Add(text);
                recent.Children.Add(row);
            }
            var right = Box(recent);
            Grid.SetColumn(right, 2);
            grid.Children.Add(right);
            return grid;
        }

        static FrameworkElement Box(UIElement content)
        {
            var g = new Grid();
            g.Children.Add(new Rectangle
            {
                Stroke = Theme.AccentBrush, StrokeThickness = 1, Opacity = 0.75,
                StrokeDashArray = new DoubleCollection { 3, 3 }, RadiusX = 4, RadiusY = 4,
            });
            g.Children.Add(content);
            return g;
        }

        static FrameworkElement Separator(double topBottom) => new Rectangle
        {
            Height = 1,
            Fill = Theme.LineBrush,
            Margin = new Thickness(8, topBottom, 8, topBottom),
        };

        Row ActionRow(string text, Action click) => new(text, null, () =>
        {
            IsOpen = false;
            click();
        });

        Row ToggleRow(string text, Func<bool> get, Action<bool> set) =>
            new(text, () => get(), () => set(!get()));

        sealed class Row : Border
        {
            readonly TextBlock _mark;
            readonly Func<bool?> _state;
            readonly Action _click;

            public Row(string text, Func<bool?> state, Action click)
            {
                _state = state;
                _click = click;
                Background = Brushes.Transparent;
                CornerRadius = new CornerRadius(3);
                Padding = new Thickness(8, 3, 8, 3);
                Cursor = Cursors.Hand;

                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                _mark = new TextBlock { Text = Marker(), Foreground = Theme.DimBrush, FontFamily = Theme.Font, FontSize = 12.5 };
                var label = new TextBlock { Text = text, FontFamily = Theme.Font, FontSize = 12.5 };
                Grid.SetColumn(label, 1);
                g.Children.Add(_mark);
                g.Children.Add(label);
                Child = g;

                MouseEnter += (_, _) =>
                {
                    Background = Theme.AccentSoftBrush;
                    _mark.Foreground = Theme.AccentBrush;
                    label.Foreground = Theme.AccentBrush;
                    if (_state == null) _mark.Text = "›";
                };
                MouseLeave += (_, _) =>
                {
                    Background = Brushes.Transparent;
                    _mark.Foreground = Theme.DimBrush;
                    label.Foreground = Theme.FgBrush;
                    Refresh();
                };
                MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    _click?.Invoke();
                    Refresh();
                };
            }

            string Marker() => _state == null ? "›" : (_state() == true ? "[x]" : "[ ]");

            public void Refresh()
            {
                _mark.Text = Marker();
                _mark.Foreground = _state?.Invoke() == true ? Theme.AccentBrush : Theme.DimBrush;
            }
        }
    }

    /// <summary>Небольшое окно-сообщение в той же терминальной стилистике.</summary>
    public static class ClaudeDialog
    {
        public static void Info(string title, string text)
        {
            var w = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                FontFamily = Theme.Font,
                FontSize = 12.5,
                Resources = Theme.Load(),
            };

            var root = new Grid { Margin = new Thickness(12), Width = 600 };
            root.Children.Add(new Border { Background = Theme.BgBrush, CornerRadius = new CornerRadius(6), Opacity = 0.985 });
            root.Children.Add(new Rectangle
            {
                Stroke = Theme.AccentBrush, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 }, RadiusX = 6, RadiusY = 6,
            });
            root.Children.Add(new TextBlock
            {
                Text = " " + title + " ",
                Foreground = Theme.AccentBrush,
                Background = Theme.BgBrush,
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 12,
            });

            var body = new StackPanel { Margin = new Thickness(20, 30, 20, 16) };
            body.Children.Add(new ScrollViewer
            {
                MaxHeight = 460,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 19 },
            });
            var close = new TextBlock
            {
                Text = "[ закрыть ]",
                Foreground = Theme.AccentBrush,
                Margin = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand,
            };
            close.MouseLeftButtonUp += (_, _) => w.Close();
            body.Children.Add(close);
            root.Children.Add(body);

            w.Content = root;
            w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
            w.ShowDialog();
        }

        public static bool Confirm(string title, string text)
        {
            bool result = false;
            var w = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                FontFamily = Theme.Font,
                FontSize = 12.5,
                Resources = Theme.Load(),
            };

            var root = new Grid { Margin = new Thickness(12), Width = 480 };
            root.Children.Add(new Border { Background = Theme.BgBrush, CornerRadius = new CornerRadius(6), Opacity = 0.985 });
            root.Children.Add(new Rectangle
            {
                Stroke = Theme.AccentBrush, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 }, RadiusX = 6, RadiusY = 6,
            });
            root.Children.Add(new TextBlock
            {
                Text = " " + title + " ",
                Foreground = Theme.AccentBrush,
                Background = Theme.BgBrush,
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 12,
            });

            var body = new StackPanel { Margin = new Thickness(20, 30, 20, 16) };
            body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 19 });
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
            };
            var yes = new TextBlock { Text = "[ да ]", Foreground = Theme.AccentBrush, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 16, 0) };
            var no = new TextBlock { Text = "[ нет ]", Foreground = Theme.DimBrush, Cursor = Cursors.Hand };
            yes.MouseLeftButtonUp += (_, _) => { result = true; w.Close(); };
            no.MouseLeftButtonUp += (_, _) => { result = false; w.Close(); };
            buttons.Children.Add(yes);
            buttons.Children.Add(no);
            body.Children.Add(buttons);
            root.Children.Add(body);

            w.Content = root;
            w.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) w.Close(); };
            w.ShowDialog();
            return result;
        }
    }
}
