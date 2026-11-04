using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfImage = System.Windows.Controls.Image;

namespace ClawdPet
{
    /// <summary>Базовое прозрачное окно поверх всех, не крадущее фокус. Позиция — в физических пикселях.</summary>
    public class OverlayWindow : Window
    {
        public IntPtr Hwnd { get; private set; }
        public double Scale { get; protected set; } = 1.0;
        public int PxX, PxY, PxW = 1, PxH = 1;

        public OverlayWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Focusable = false;
            UseLayoutRounding = true;
            Left = -32000; Top = -32000; Width = 1; Height = 1;
            SourceInitialized += (_, _) =>
            {
                Hwnd = new WindowInteropHelper(this).Handle;
                Native.MakeOverlay(Hwnd);
                Scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            };
        }

        public void Place(int x, int y, int w, int h)
        {
            PxX = x; PxY = y; PxW = w; PxH = h;
            if (Hwnd != IntPtr.Zero) Native.Place(Hwnd, x, y, w, h);
        }

        public void MoveTo(int x, int y)
        {
            if (x == PxX && y == PxY) return;
            PxX = x; PxY = y;
            if (Hwnd != IntPtr.Zero) Native.Move(Hwnd, x, y);
        }

        public void KeepOnTop()
        {
            if (Hwnd != IntPtr.Zero && IsVisible) Native.BringTop(Hwnd);
        }
    }

    /// <summary>Маленькое окошко с одной картинкой: печенька или кучка.</summary>
    public class SpriteWindow : OverlayWindow
    {
        public readonly WpfImage Img;
        readonly Canvas _root;
        readonly ScaleTransform _scale = new(1, 1);
        readonly RotateTransform _rot = new(0);
        readonly int _imgW, _imgH, _padX, _padTop, _padBottom;
        DispatcherTimer _tween;
        public double X, Y; // левый верхний угол картинки, px
        public Action Clicked;

        public SpriteWindow(BitmapSource bmp, double scale, int padX, int padTop, int padBottom)
        {
            Scale = scale;
            _imgW = bmp.PixelWidth; _imgH = bmp.PixelHeight;
            _padX = padX; _padTop = padTop; _padBottom = padBottom;
            _root = new Canvas();
            Img = new WpfImage
            {
                Source = bmp,
                Stretch = Stretch.Fill,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new TransformGroup { Children = { _scale, _rot } },
                Cursor = Cursors.Hand,
            };
            RenderOptions.SetBitmapScalingMode(Img, BitmapScalingMode.NearestNeighbor);
            _root.Children.Add(Img);
            Content = _root;
            Img.MouseLeftButtonUp += (_, e) => { e.Handled = true; Clicked?.Invoke(); };
            SourceInitialized += (_, _) => Relayout();
        }

        void Relayout()
        {
            Img.Width = _imgW / Scale; Img.Height = _imgH / Scale;
            Canvas.SetLeft(Img, _padX / Scale);
            Canvas.SetTop(Img, _padTop / Scale);
            Place((int)Math.Round(X) - _padX, (int)Math.Round(Y) - _padTop, _imgW + _padX * 2, _imgH + _padTop + _padBottom);
        }

        public int ImgW => _imgW;
        public int ImgH => _imgH;
        public double CenterX => X + _imgW / 2.0;
        public double Bottom => Y + _imgH;

        public void SetPos(double x, double y)
        {
            X = x; Y = y;
            MoveTo((int)Math.Round(X) - _padX, (int)Math.Round(Y) - _padTop);
        }

        public void SetLook(double scale, double rot, double opacity)
        {
            _scale.ScaleX = _scale.ScaleY = scale;
            _rot.Angle = rot;
            Img.Opacity = opacity;
        }

        /// <summary>Анимация позиции/масштаба/поворота/прозрачности с ускорением или «пружинкой».</summary>
        public void Animate(double toX, double toY, double toScale, double toRot, double toOpacity, double seconds,
            string ease, Action done)
        {
            _tween?.Stop();
            double fx = X, fy = Y, fs = _scale.ScaleX, fr = _rot.Angle, fo = Img.Opacity;
            var start = DateTime.UtcNow;
            _tween = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
            _tween.Tick += (_, _) =>
            {
                double t = Math.Min(1.0, (DateTime.UtcNow - start).TotalSeconds / Math.Max(0.01, seconds));
                double k = ease switch
                {
                    "accel" => Math.Pow(t, 3.2),
                    "overshoot" => 1 + 3 * Math.Pow(t - 1, 3) + 2 * Math.Pow(t - 1, 2),
                    _ => t,
                };
                double sk = ease == "overshoot" ? k : t;
                SetPos(fx + (toX - fx) * (ease == "overshoot" ? t : k), fy + (toY - fy) * (ease == "overshoot" ? t : k));
                SetLook(fs + (toScale - fs) * sk, fr + (toRot - fr) * k, fo + (toOpacity - fo) * t);
                if (t >= 1.0)
                {
                    _tween.Stop();
                    _tween = null;
                    done?.Invoke();
                }
            };
            _tween.Start();
        }

        /// <summary>Искорки, вылетающие вверх из окна (место под них — в верхнем отступе).</summary>
        public void Burst(BitmapSource spark, int count, Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                var img = new WpfImage { Source = spark, Width = spark.PixelWidth / Scale, Height = spark.PixelHeight / Scale, IsHitTestVisible = false };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                double sx = (_padX + _imgW / 2.0 - spark.PixelWidth / 2.0) / Scale + rng.NextDouble() * 16 - 8;
                double sy = (_padTop + _imgH / 2.0 - spark.PixelHeight) / Scale;
                Canvas.SetLeft(img, sx);
                Canvas.SetTop(img, sy);
                var tt = new TranslateTransform();
                img.RenderTransform = tt;
                _root.Children.Add(img);
                var dur = TimeSpan.FromMilliseconds(700);
                tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, rng.NextDouble() * 32 - 16, dur));
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -(30 + rng.NextDouble() * 15), dur));
                var fade = new DoubleAnimation(1, 0, dur);
                fade.Completed += (_, _) => _root.Children.Remove(img);
                img.BeginAnimation(OpacityProperty, fade);
            }
        }
    }

    /// <summary>Окно самого питомца: спрайт, пузырь с репликой, частицы.</summary>
    public class PetWindow : OverlayWindow
    {
        public readonly Canvas Root = new();
        public readonly WpfImage PetImg;
        readonly RotateTransform _rot = new(0);
        public readonly Border Bubble;
        readonly TextBlock _bubbleText;
        int _bubbleGen;
        public int ML, MT, PetW, PetH;

        public event Action<MouseButtonEventArgs> PetDown;
        public event Action PetMove;
        public event Action<bool> PetUp; // cancelled?
        public event Action PetMenu;
        public event Action<double> ScaleChanged;
        bool _pressed;

        public PetWindow()
        {
            PetImg = new WpfImage
            {
                Stretch = Stretch.Fill,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _rot,
                Cursor = Cursors.Hand,
            };
            RenderOptions.SetBitmapScalingMode(PetImg, BitmapScalingMode.NearestNeighbor);

            _bubbleText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 230,
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F)),
                TextAlignment = TextAlignment.Center,
            };
            Bubble = new Border
            {
                Child = _bubbleText,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0xD9, 0xD9)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 6, 10, 7),
                Visibility = Visibility.Collapsed,
                Opacity = 0,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.18 },
            };
            Bubble.MouseLeftButtonDown += (_, e) => { e.Handled = true; HideBubble(); };

            Root.Children.Add(Bubble);
            Root.Children.Add(PetImg);
            Content = Root;

            PetImg.MouseLeftButtonDown += (_, e) =>
            {
                e.Handled = true;
                _pressed = true;
                CaptureMouse();
                PetDown?.Invoke(e);
            };
            MouseMove += (_, _) => { if (_pressed) PetMove?.Invoke(); };
            MouseLeftButtonUp += (_, e) =>
            {
                if (!_pressed) return;
                _pressed = false;
                ReleaseMouseCapture();
                PetUp?.Invoke(false);
            };
            LostMouseCapture += (_, _) =>
            {
                if (!_pressed) return;
                _pressed = false;
                PetUp?.Invoke(true);
            };
            PetImg.MouseRightButtonUp += (_, e) => { e.Handled = true; PetMenu?.Invoke(); };
            DpiChanged += (_, e) =>
            {
                Scale = e.NewDpi.DpiScaleX;
                ScaleChanged?.Invoke(Scale);
            };
        }

        public void Layout(int petW, int petH)
        {
            PetW = petW; PetH = petH;
            ML = (int)(180 * Scale);
            MT = (int)(170 * Scale);
            int mb = (int)(110 * Scale);
            PetImg.Width = petW / Scale;
            PetImg.Height = petH / Scale;
            Canvas.SetLeft(PetImg, ML / Scale);
            Canvas.SetTop(PetImg, MT / Scale);
            Place(PxX, PxY, petW + ML * 2, petH + MT + mb);
        }

        public void SetFrame(BitmapSource bmp) => PetImg.Source = bmp;
        public void SetRotation(double deg) { if (Math.Abs(_rot.Angle - deg) > 0.01) _rot.Angle = deg; }

        public bool BubbleVisible { get; private set; }

        public void ShowBubble(string text)
        {
            _bubbleGen++;
            _bubbleText.Text = text;
            Bubble.Visibility = Visibility.Visible;
            Bubble.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            BubbleVisible = true;
        }

        public void HideBubble()
        {
            if (!BubbleVisible) return;
            BubbleVisible = false;
            int gen = ++_bubbleGen;
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
            fade.Completed += (_, _) => { if (gen == _bubbleGen) Bubble.Visibility = Visibility.Collapsed; };
            Bubble.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>Размер пузыря в пикселях.</summary>
        public (double W, double H) BubbleSize()
        {
            Bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return (Bubble.DesiredSize.Width * Scale, Bubble.DesiredSize.Height * Scale);
        }

        public void SetBubbleLocal(double pxX, double pxY)
        {
            Canvas.SetLeft(Bubble, pxX / Scale);
            Canvas.SetTop(Bubble, pxY / Scale);
        }

        readonly List<UIElement> _particles = new();
        public int ParticleCount => _particles.Count;

        public void AddParticle(BitmapSource bmp, double lx, double ly, double drift, double rise, double rot, double seconds)
        {
            var img = new WpfImage
            {
                Source = bmp,
                Width = bmp.PixelWidth / Scale,
                Height = bmp.PixelHeight / Scale,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            var st = new ScaleTransform(0.5, 0.5);
            var tt = new TranslateTransform();
            img.RenderTransform = new TransformGroup { Children = { st, new RotateTransform(rot), tt } };
            Canvas.SetLeft(img, lx / Scale);
            Canvas.SetTop(img, ly / Scale);
            Root.Children.Insert(0, img);
            _particles.Add(img);
            var dur = TimeSpan.FromSeconds(seconds);
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, drift / Scale, dur));
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -rise / Scale, dur));
            st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.5, 1, dur));
            st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.5, 1, dur));
            var fade = new DoubleAnimation(1, 0, dur);
            fade.Completed += (_, _) => { Root.Children.Remove(img); _particles.Remove(img); };
            img.BeginAnimation(OpacityProperty, fade);
        }
    }
}
