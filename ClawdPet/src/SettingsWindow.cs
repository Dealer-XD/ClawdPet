using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClawdPet
{
    /// <summary>Окно настроек — аналог create_settings из плагина.</summary>
    public class SettingsWindow : Window
    {
        readonly Config _cfg;
        readonly Pet _pet;

        public SettingsWindow(Config cfg, Pet pet)
        {
            _cfg = cfg;
            _pet = pet;
            Title = "Clawd — настройки";
            Width = 500;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                pet.MakeTrayIcon().Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

            var tabs = new TabControl { Margin = new Thickness(8) };
            tabs.Items.Add(new TabItem { Header = "Питомец", Content = Scroll(PetTab()) });
            tabs.Items.Add(new TabItem { Header = "ИИ", Content = Scroll(AiTab()) });
            tabs.Items.Add(new TabItem { Header = "Мозг", Content = Scroll(BrainTab()) });
            tabs.Items.Add(new TabItem { Header = "О программе", Content = Scroll(AboutTab()) });
            Content = tabs;
            Closed += (_, _) => _pet.ConfigChanged(false);
        }

        static ScrollViewer Scroll(UIElement e) => new() { Content = e, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        StackPanel Panel() => new() { Margin = new Thickness(12) };

        void Header(StackPanel p, string text) =>
            p.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 12, 0, 4) });

        void Hint(StackPanel p, string text) =>
            p.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 6), FontSize = 12 });

        void Check(StackPanel p, string label, Func<bool> get, Action<bool> set, bool visuals = false)
        {
            var cb = new CheckBox { Content = label, IsChecked = get(), Margin = new Thickness(0, 4, 0, 4) };
            cb.Click += (_, _) => { set(cb.IsChecked == true); _pet.ConfigChanged(visuals); };
            p.Children.Add(cb);
        }

        void Combo(StackPanel p, string label, string[] items, Func<int> get, Action<int> set, bool visuals = false)
        {
            p.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 2) });
            var cb = new ComboBox { ItemsSource = items, SelectedIndex = Math.Clamp(get(), 0, items.Length - 1), Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
            cb.SelectionChanged += (_, _) => { set(cb.SelectedIndex); _pet.ConfigChanged(visuals); };
            p.Children.Add(cb);
        }

        void Text(StackPanel p, string label, Func<string> get, Action<string> set, bool secret = false, bool multi = false)
        {
            p.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 6, 0, 2) });
            if (secret)
            {
                var pb = new PasswordBox { Password = get() ?? "", Width = 420, HorizontalAlignment = HorizontalAlignment.Left };
                pb.PasswordChanged += (_, _) => set(pb.Password.Trim());
                pb.LostFocus += (_, _) => _pet.ConfigChanged(false);
                p.Children.Add(pb);
                return;
            }
            var tb = new TextBox
            {
                Text = get() ?? "", Width = 420, HorizontalAlignment = HorizontalAlignment.Left,
                AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap,
                Height = multi ? 60 : double.NaN,
            };
            tb.TextChanged += (_, _) => set(tb.Text.Trim());
            tb.LostFocus += (_, _) => _pet.ConfigChanged(false);
            p.Children.Add(tb);
        }

        void Button(StackPanel p, string label, Action click)
        {
            var b = new Button { Content = label, Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            b.Click += (_, _) => click();
            p.Children.Add(b);
        }

        UIElement PetTab()
        {
            var p = Panel();
            Header(p, "Внешний вид");
            Check(p, "Показывать Clawd", () => _cfg.Show, v => _cfg.Show = v);
            Combo(p, "Размер", C.SizeItems, () => _cfg.Size, v => _cfg.Size = v, true);
            var colors = Array.ConvertAll(C.Palettes, x => x.Name);
            Combo(p, "Цвет", colors, () => _cfg.Color, v => _cfg.Color = v, true);

            Header(p, "Поведение");
            Check(p, "Гравитация (стоит на панели задач и падает)", () => _cfg.Gravity, v => _cfg.Gravity = v);
            Check(p, "Стоит на окнах и катается на них", () => _cfg.RideWindows, v => _cfg.RideWindows = v);
            Check(p, "Слетает, если потрясти окно под ним", () => _cfg.ShakeWindows, v => _cfg.ShakeWindows = v);
            Check(p, "Гуляет сам", () => _cfg.Wander, v => _cfg.Wander = v);
            Check(p, "Реплики", () => _cfg.Phrases, v => _cfg.Phrases = v);
            Combo(p, "Засыпает", C.SleepItems, () => _cfg.Sleep, v => _cfg.Sleep = v);
            Check(p, "Танцует под музыку (любой плеер Windows)", () => _cfg.Dance, v => _cfg.Dance = v);
            Check(p, "Комментирует открытые программы", () => _cfg.AppEvents, v => _cfg.AppEvents = v);
            Check(p, "Оставляет кучки, если переест", () => _cfg.Poop, v => _cfg.Poop = v);
            Check(p, "Прятаться, когда игра или видео на весь экран", () => _cfg.HideFullscreen, v => _cfg.HideFullscreen = v);

            Header(p, "Система");
            Check(p, "Запускать вместе с Windows", () => Config.Autostart, v => Config.Autostart = v);
            Button(p, "Позвать к курсору", () => _pet.Recenter());
            return p;
        }

        UIElement AiTab()
        {
            var p = Panel();
            Header(p, "ИИ-реплики");
            Hint(p, "Clawd может придумывать реплики сам. Бесплатный ключ Gemini: aistudio.google.com/apikey. " +
                    "Подойдёт и любой OpenAI-совместимый сервер (OpenRouter, LM Studio, Ollama).");
            Check(p, "Включить ИИ", () => _cfg.AiEnabled, v => _cfg.AiEnabled = v);
            Combo(p, "Провайдер", C.AiProviders, () => _cfg.AiProvider, v => _cfg.AiProvider = v);
            Text(p, "API-ключ", () => _cfg.AiKey, v => _cfg.AiKey = v, secret: true);
            Text(p, "Модель (пусто — по умолчанию)", () => _cfg.AiModel, v => _cfg.AiModel = v);
            Text(p, "Адрес API (только для OpenAI-совместимого)", () => _cfg.AiUrl, v => _cfg.AiUrl = v);
            Combo(p, "Как часто болтает сам", C.AiChatItems, () => _cfg.AiChat, v => _cfg.AiChat = v);
            Check(p, "ИИ выбирает эмоции", () => _cfg.AiMoods, v => _cfg.AiMoods = v);
            Check(p, "ИИ решает, куда пойти", () => _cfg.AiMove, v => _cfg.AiMove = v);
            Check(p, "Передавать ИИ заголовки окон", () => _cfg.AiTitles, v => _cfg.AiTitles = v);
            Check(p, "Обычные фразы, если ИИ недоступен", () => _cfg.AiFallback, v => _cfg.AiFallback = v);
            Text(p, "Характер (дополнение к личности)", () => _cfg.AiPersona, v => _cfg.AiPersona = v, multi: true);

            Header(p, "Запасной провайдер (OpenAI-совместимый)");
            Text(p, "Адрес API", () => _cfg.Ai2Url, v => _cfg.Ai2Url = v);
            Text(p, "API-ключ", () => _cfg.Ai2Key, v => _cfg.Ai2Key = v, secret: true);
            Text(p, "Модель", () => _cfg.Ai2Model, v => _cfg.Ai2Model = v);
            Button(p, "Проверить ИИ", () => { _pet.ConfigChanged(false); _pet.AiTest(); });
            return p;
        }

        UIElement BrainTab()
        {
            var p = Panel();
            Header(p, "Память");
            Hint(p, "Clawd помнит тебя: имя, привычки, любимые программы и треки, что вы делали вместе, " +
                    "копит привязанность и сам запоминает новые факты. Всё хранится только на этом компьютере.");
            Check(p, "Включить память", () => _cfg.BrainEnabled, v => _cfg.BrainEnabled = v);
            Text(p, "Как тебя зовут", () => _cfg.BrainName, v => _cfg.BrainName = v);
            Text(p, "О себе (для ИИ)", () => _cfg.BrainAbout, v => _cfg.BrainAbout = v, multi: true);
            Check(p, "Запоминать новые факты от ИИ", () => _cfg.BrainLearn, v => _cfg.BrainLearn = v);
            Check(p, "Делиться памятью с ИИ", () => _cfg.BrainShare, v => _cfg.BrainShare = v);
            Button(p, "Что Clawd помнит", () => MessageBox.Show(this, _pet.DescribeMemory(), "Память Clawd"));
            Button(p, "Перенести память из плагина (clawd_brain.json)", ImportBrain);
            Button(p, "Открыть папку с данными", () =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Config.Dir) { UseShellExecute = true }));
            Button(p, "Забыть всё", () =>
            {
                if (MessageBox.Show(this, "Clawd забудет всё, что знал о тебе. Точно?", "Забыть всё",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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
                MessageBox.Show(this, _pet.BrainImport(raw) ? "Готово! Clawd всё вспомнил." : "Не получилось прочитать файл.", "Импорт памяти");
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, "Импорт памяти");
            }
        }

        UIElement AboutTab()
        {
            var p = Panel();
            Header(p, "Clawd для Windows 2.0");
            Hint(p, "Пиксельный питомец Claude, который живёт поверх окон.\n\n" +
                    "Клик — ткнуть, двойной клик — прыжок, зажать и водить туда-сюда — погладить, " +
                    "перетащить — перенести, резко бросить — полёт. Правый клик по Clawd — меню.\n\n" +
                    "Стоит на панели задач и на верхних краях окон, катается, если двигать окно, " +
                    "и слетает, если окно потрясти. Подброшенный к верху экрана — повиснет на руках. " +
                    "Танцует под музыку, комментирует программы, ест печеньки и иногда оставляет кучки — кликни, чтобы убрать.\n\n" +
                    "Порт плагина для exteraGram (оригинал — @itskotovski, форк — @Skorvash).");
            return p;
        }
    }
}
