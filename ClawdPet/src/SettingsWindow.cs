using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClawdPet
{
    /// <summary>Окно настроек в терминальной стилистике Claude Code.</summary>
    public class SettingsWindow : Window
    {
        readonly Config _cfg;
        readonly Pet _pet;

        public SettingsWindow(Config cfg, Pet pet)
        {
            _cfg = cfg;
            _pet = pet;
            Title = "Claude Code — Clawd";
            Width = 580;
            Height = 740;
            MinWidth = 520;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = Theme.Font;
            FontSize = 12.5;
            Resources = Theme.Load();
            Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                pet.MakeTrayIcon().Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

            var root = new Grid { Margin = new Thickness(14) };
            root.Children.Add(new Border { Background = Theme.BgBrush, CornerRadius = new CornerRadius(8), Opacity = 0.985 });
            root.Children.Add(new Rectangle
            {
                Stroke = Theme.AccentBrush, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 }, RadiusX = 8, RadiusY = 8,
            });

            var layout = new Grid { Margin = new Thickness(18, 12, 18, 16) };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.Children.Add(Chrome());
            var tabs = Tabs();
            Grid.SetRow(tabs, 1);
            layout.Children.Add(tabs);
            root.Children.Add(layout);
            Content = root;

            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
            Closed += (_, _) => _pet.ConfigChanged(false);
        }

        FrameworkElement Chrome()
        {
            var grid = new Grid { Margin = new Thickness(2, 0, 2, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new StackPanel { Orientation = Orientation.Horizontal };
            title.Children.Add(new TextBlock { Text = "Claude Code v" + C.Version, Foreground = Theme.AccentBrush, FontSize = 13 });
            title.Children.Add(new TextBlock { Text = "  —  настройки Clawd", Foreground = Theme.DimBrush, FontSize = 12 });
            grid.Children.Add(title);

            var close = new TextBlock
            {
                Text = "[ закрыть ]",
                Foreground = Theme.DimBrush,
                FontSize = 12,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
            };
            close.MouseEnter += (_, _) => close.Foreground = Theme.AccentBrush;
            close.MouseLeave += (_, _) => close.Foreground = Theme.DimBrush;
            // Не даём заголовку перехватить клик: иначе вместо закрытия начнётся DragMove.
            close.MouseLeftButtonDown += (_, e) => { e.Handled = true; };
            close.MouseLeftButtonUp += (_, e) => { e.Handled = true; Close(); };
            Grid.SetColumn(close, 1);
            grid.Children.Add(close);

            grid.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ChangedButton == MouseButton.Left && Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch (InvalidOperationException) { }
                }
            };
            return grid;
        }

        TabControl Tabs()
        {
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = "питомец", Content = Scroll(PetTab()) });
            tabs.Items.Add(new TabItem { Header = "ии", Content = Scroll(AiTab()) });
            tabs.Items.Add(new TabItem { Header = "мозг", Content = Scroll(BrainTab()) });
            tabs.Items.Add(new TabItem { Header = "о программе", Content = Scroll(AboutTab()) });
            return tabs;
        }

        static ScrollViewer Scroll(UIElement e) => new()
        {
            Content = e,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };

        StackPanel Panel() => new() { Margin = new Thickness(2, 0, 10, 12) };

        void Header(StackPanel p, string text)
        {
            if (p.Children.Count > 0)
                p.Children.Add(new Rectangle { Height = 1, Fill = Theme.LineBrush, Margin = new Thickness(0, 14, 0, 8) });
            p.Children.Add(new TextBlock
            {
                Text = text,
                Foreground = Theme.AccentBrush,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 6),
            });
        }

        void Hint(StackPanel p, string text) =>
            p.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Theme.DimBrush,
                Margin = new Thickness(0, 0, 0, 8),
                FontSize = 11.5,
                LineHeight = 17,
            });

        void Check(StackPanel p, string label, Func<bool> get, Action<bool> set, bool visuals = false)
        {
            var cb = new CheckBox { Content = label, IsChecked = get(), Margin = new Thickness(0, 2, 0, 2) };
            cb.Click += (_, _) => { set(cb.IsChecked == true); _pet.ConfigChanged(visuals); };
            p.Children.Add(cb);
        }

        ComboBox Combo(StackPanel p, string label, string[] items, Func<int> get, Action<int> set, bool visuals = false)
        {
            p.Children.Add(new TextBlock { Text = label, Foreground = Theme.DimBrush, Margin = new Thickness(0, 8, 0, 4) });
            var cb = new ComboBox
            {
                ItemsSource = items,
                SelectedIndex = Math.Clamp(get(), 0, items.Length - 1),
                Width = 320,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            cb.SelectionChanged += (_, _) => { set(cb.SelectedIndex); _pet.ConfigChanged(visuals); };
            p.Children.Add(cb);
            return cb;
        }

        TextBox Text(StackPanel p, string label, Func<string> get, Action<string> set, bool secret = false, bool multi = false)
        {
            p.Children.Add(new TextBlock { Text = label, Foreground = Theme.DimBrush, Margin = new Thickness(0, 8, 0, 4) });
            if (secret)
            {
                var pb = new PasswordBox { Password = get() ?? "", HorizontalAlignment = HorizontalAlignment.Stretch };
                pb.PasswordChanged += (_, _) => set(pb.Password.Trim());
                pb.LostFocus += (_, _) => _pet.ConfigChanged(false);
                p.Children.Add(pb);
                return null;
            }
            var tb = new TextBox
            {
                Text = get() ?? "",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                AcceptsReturn = multi,
                TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap,
                Height = multi ? 64 : double.NaN,
                VerticalContentAlignment = VerticalAlignment.Top,
            };
            tb.TextChanged += (_, _) => set(tb.Text.Trim());
            tb.LostFocus += (_, _) => _pet.ConfigChanged(false);
            p.Children.Add(tb);
            return tb;
        }

        void Button(StackPanel p, string label, Action click)
        {
            var b = new Button
            {
                Content = label,
                Padding = new Thickness(14, 5, 14, 5),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            b.Click += (_, _) => click();
            p.Children.Add(b);
        }

        UIElement PetTab()
        {
            var p = Panel();
            Header(p, "внешний вид");
            Check(p, "Показывать Clawd", () => _cfg.Show, v => _cfg.Show = v);
            Combo(p, "Размер", C.SizeItems, () => _cfg.Size, v => _cfg.Size = v, true);
            var colors = Array.ConvertAll(C.Palettes, x => x.Name);
            Combo(p, "Цвет", colors, () => _cfg.Color, v => _cfg.Color = v, true);

            Header(p, "поведение");
            Combo(p, "Высота прыжка", C.JumpItems, () => _cfg.Jump, v => _cfg.Jump = v);
            Check(p, "Гравитация (стоит на панели задач и падает)", () => _cfg.Gravity, v => _cfg.Gravity = v);
            Check(p, "Стоит на окнах и катается на них", () => _cfg.RideWindows, v => _cfg.RideWindows = v);
            Check(p, "Слетает, если потрясти окно под ним", () => _cfg.ShakeWindows, v => _cfg.ShakeWindows = v);
            Check(p, "Гуляет сам", () => _cfg.Wander, v => _cfg.Wander = v);
            Check(p, "Реплики", () => _cfg.Phrases, v => _cfg.Phrases = v);
            Check(p, "Сам таскает иконки с панели задач", () => _cfg.EatIcons, v => _cfg.EatIcons = v);
            Combo(p, "Засыпает", C.SleepItems, () => _cfg.Sleep, v => _cfg.Sleep = v);
            Check(p, "Танцует под музыку (любой плеер Windows)", () => _cfg.Dance, v => _cfg.Dance = v);
            Check(p, "Комментирует открытые программы", () => _cfg.AppEvents, v => _cfg.AppEvents = v);
            Check(p, "Оставляет кучки, если переел иконок", () => _cfg.Poop, v => _cfg.Poop = v);
            Check(p, "Прятаться, когда игра или видео на весь экран", () => _cfg.HideFullscreen, v => _cfg.HideFullscreen = v);

            Header(p, "система");
            Check(p, "Запускать вместе с Windows", () => Config.Autostart, v => Config.Autostart = v);
            Button(p, "Позвать к курсору", () => _pet.Recenter());
            return p;
        }

        TextBlock HintLine(StackPanel p)
        {
            var t = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Theme.DimBrush,
                Margin = new Thickness(0, 2, 0, 6),
                FontSize = 11.5,
                LineHeight = 17,
            };
            p.Children.Add(t);
            return t;
        }

        static int PresetIndex(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return C.AiPresets.Length - 1;
            string norm = url.Trim().TrimEnd('/').ToLowerInvariant();
            for (int i = 0; i < C.AiPresets.Length; i++)
            {
                var p = C.AiPresets[i];
                if (!string.IsNullOrWhiteSpace(p.Url) &&
                    p.Url.Trim().TrimEnd('/').ToLowerInvariant() == norm)
                    return i;
            }
            return C.AiPresets.Length - 1;
        }

        static void SetPresetHint(TextBlock hint, int index)
        {
            var p = C.AiPresets[Math.Clamp(index, 0, C.AiPresets.Length - 1)];
            hint.Text = string.IsNullOrWhiteSpace(p.Url)
                ? "Свой сервер: заполни адрес, модель и ключ вручную."
                : p.NeedsKey
                    ? "Этому сервису нужен API-ключ."
                    : "Ключ не нужен — оставь поле пустым.";
        }

        static void SetModelHint(TextBlock hint, TextBox model)
        {
            bool empty = string.IsNullOrWhiteSpace(model.Text);
            hint.Text = empty
                ? "Укажи модель вручную — список моделей есть в личном кабинете сервиса."
                : "";
            hint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        }

        UIElement AiTab()
        {
            var p = Panel();
            Header(p, "ии-реплики");
            Hint(p, "Clawd может придумывать реплики сам. Выбери сервис из списка — адрес и модель " +
                    "подставятся автоматически, но поля останутся редактируемыми. Для Gemini нужен " +
                    "бесплатный ключ: aistudio.google.com/apikey.");
            Check(p, "Включить ИИ", () => _cfg.AiEnabled, v => _cfg.AiEnabled = v);
            var provider = Combo(p, "Провайдер", C.AiProviders, () => _cfg.AiProvider, v => _cfg.AiProvider = v);

            // Пресеты для основного провайдера.
            p.Children.Add(new TextBlock { Text = "Сервис (пресет)", Foreground = Theme.DimBrush, Margin = new Thickness(0, 8, 0, 4) });
            var preset = new ComboBox
            {
                ItemsSource = Array.ConvertAll(C.AiPresets, x => x.Name),
                SelectedIndex = PresetIndex(_cfg.AiUrl),
                Width = 320,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            p.Children.Add(preset);
            var presetHint = HintLine(p);

            Text(p, "API-ключ", () => _cfg.AiKey, v => _cfg.AiKey = v, secret: true);
            var model = Text(p, "Модель (пусто — укажи вручную)", () => _cfg.AiModel, v => _cfg.AiModel = v);
            var modelHint = HintLine(p);
            var url = Text(p, "Адрес API", () => _cfg.AiUrl, v => _cfg.AiUrl = v);

            preset.SelectionChanged += (_, _) => ApplyPreset(preset.SelectedIndex);
            model.TextChanged += (_, _) => SetModelHint(modelHint, model);
            SetPresetHint(presetHint, preset.SelectedIndex);
            SetModelHint(modelHint, model);

            void ApplyPreset(int i)
            {
                if (i < 0 || i >= C.AiPresets.Length) return;
                var pr = C.AiPresets[i];
                SetPresetHint(presetHint, i);
                if (string.IsNullOrWhiteSpace(pr.Url)) return;
                // Пресеты — это OpenAI-совместимые сервисы, переключаем провайдера автоматически.
                _cfg.AiProvider = 1;
                provider.SelectedIndex = 1;
                _cfg.AiUrl = pr.Url;
                _cfg.AiModel = pr.Model;
                url.Text = pr.Url;
                model.Text = pr.Model;
                _pet.ConfigChanged(false);
            }

            Combo(p, "Как часто болтает сам", C.AiChatItems, () => _cfg.AiChat, v => _cfg.AiChat = v);
            Check(p, "ИИ выбирает эмоции", () => _cfg.AiMoods, v => _cfg.AiMoods = v);
            Check(p, "ИИ решает, куда пойти", () => _cfg.AiMove, v => _cfg.AiMove = v);
            Check(p, "Передавать ИИ заголовки окон", () => _cfg.AiTitles, v => _cfg.AiTitles = v);
            Check(p, "Обычные фразы, если ИИ недоступен", () => _cfg.AiFallback, v => _cfg.AiFallback = v);
            Text(p, "Характер (дополнение к личности)", () => _cfg.AiPersona, v => _cfg.AiPersona = v, multi: true);

            Header(p, "запасной провайдер (openai-совместимый)");
            p.Children.Add(new TextBlock { Text = "Сервис (пресет)", Foreground = Theme.DimBrush, Margin = new Thickness(0, 8, 0, 4) });
            var preset2 = new ComboBox
            {
                ItemsSource = Array.ConvertAll(C.AiPresets, x => x.Name),
                SelectedIndex = PresetIndex(_cfg.Ai2Url),
                Width = 320,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            p.Children.Add(preset2);
            var preset2Hint = HintLine(p);

            var url2 = Text(p, "Адрес API", () => _cfg.Ai2Url, v => _cfg.Ai2Url = v);
            Text(p, "API-ключ", () => _cfg.Ai2Key, v => _cfg.Ai2Key = v, secret: true);
            var model2 = Text(p, "Модель (пусто — укажи вручную)", () => _cfg.Ai2Model, v => _cfg.Ai2Model = v);
            var model2Hint = HintLine(p);

            preset2.SelectionChanged += (_, _) => ApplyPreset2(preset2.SelectedIndex);
            model2.TextChanged += (_, _) => SetModelHint(model2Hint, model2);
            SetPresetHint(preset2Hint, preset2.SelectedIndex);
            SetModelHint(model2Hint, model2);

            void ApplyPreset2(int i)
            {
                if (i < 0 || i >= C.AiPresets.Length) return;
                var pr = C.AiPresets[i];
                SetPresetHint(preset2Hint, i);
                if (string.IsNullOrWhiteSpace(pr.Url)) return;
                _cfg.Ai2Url = pr.Url;
                _cfg.Ai2Model = pr.Model;
                url2.Text = pr.Url;
                model2.Text = pr.Model;
                _pet.ConfigChanged(false);
            }

            Button(p, "Проверить ИИ", () => { _pet.ConfigChanged(false); _pet.AiTest(); });
            return p;
        }

        UIElement BrainTab()
        {
            var p = Panel();
            Header(p, "память");
            Hint(p, "Clawd помнит тебя: имя, привычки, любимые программы и треки, что вы делали вместе, " +
                    "копит привязанность и сам запоминает новые факты. Всё хранится только на этом компьютере.");
            Check(p, "Включить память", () => _cfg.BrainEnabled, v => _cfg.BrainEnabled = v);
            Text(p, "Как тебя зовут", () => _cfg.BrainName, v => _cfg.BrainName = v);
            Text(p, "О себе (для ИИ)", () => _cfg.BrainAbout, v => _cfg.BrainAbout = v, multi: true);
            Check(p, "Запоминать новые факты от ИИ", () => _cfg.BrainLearn, v => _cfg.BrainLearn = v);
            Check(p, "Делиться памятью с ИИ", () => _cfg.BrainShare, v => _cfg.BrainShare = v);
            Button(p, "Что Clawd помнит", () => ClaudeDialog.Info("Память Clawd", _pet.DescribeMemory()));
            Button(p, "Перенести память из плагина (clawd_brain.json)", ImportBrain);
            Button(p, "Открыть папку с данными", () =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Config.Dir) { UseShellExecute = true }));
            Button(p, "Забыть всё", () =>
            {
                if (ClaudeDialog.Confirm("Забыть всё", "Clawd забудет всё, что знал о тебе. Точно?"))
                    _pet.BrainForget();
            });
            return p;
        }

        void ImportBrain()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Память Clawd (*.json)|*.json|Все файлы|*.*" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var raw = File.ReadAllText(dlg.FileName);
                ClaudeDialog.Info("Импорт памяти",
                    _pet.BrainImport(raw) ? "Готово! Clawd всё вспомнил." : "Не получилось прочитать файл.");
            }
            catch (Exception e)
            {
                ClaudeDialog.Info("Импорт памяти", e.Message);
            }
        }

        UIElement AboutTab()
        {
            var p = Panel();
            Header(p, "Clawd для Windows " + C.Version);
            Hint(p, "Пиксельный питомец Claude, который живёт поверх окон.\n\n" +
                    "Клик — ткнуть, двойной клик — прыжок, зажать и водить туда-сюда — погладить, " +
                    "перетащить — перенести, резко бросить — полёт. Правый клик по Clawd — меню.\n\n" +
                    "Стоит на панели задач и на верхних краях окон, катается, если двигать окно, " +
                    "и слетает, если окно потрясти. Отскакивает от краёв экрана, а если поставить его " +
                    "на самый верх и отпустить — повиснет на руках. " +
                    "Танцует под музыку, комментирует программы, таскает иконки с панели задач " +
                    "и иногда оставляет кучки — кликни, чтобы убрать.\n\n" +
                    "ClawdPet — порт плагина ClawdPetPlugin для exteraGram на Windows (оригинал — @itskotovski, форк — @Skorvash).

                    Это неофициальный фанатский проект. Он не связан с Anthropic, не одобрен и не спонсируется ею. «Claude» — товарный знак Anthropic.");
            return p;
        }
    }
}
