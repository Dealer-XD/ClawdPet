using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace ClawdPet
{
    public partial class Pet
    {
        // ---------- мышь ----------

        void OnDown()
        {
            var (rx, ry) = Native.Cursor();
            double now = Now;
            bool wasSleeping = _state == "sleep", wasPeeking = _state == "peek";
            _lastInteraction = now;
            _touch = new Touch { X0 = rx, Y0 = ry, T0 = now, Ox = rx - _x, Oy = ry - _y, Anchor = rx };
            _touch.Hist.Add((now, rx, ry));
            _hopActive = false;
            _hopOff = _hopVy = 0;
            _vx = _vy = 0;
            _peekSide = _peekPending = 0;
            _state = "press";
            if (wasSleeping)
            {
                SetExpr("surprised", 0.9);
                Say("wake");
            }
            else if (wasPeeking)
            {
                _nextPeek = now + U(60.0, 120.0);
                SetExpr("surprised", 0.7);
                Say("found");
            }
            Schedule(0);
        }

        void OnMove()
        {
            var touch = _touch;
            if (touch == null) return;
            var (rx, ry) = Native.Cursor();
            double now = Now;
            var hist = touch.Hist;
            hist.Add((now, rx, ry));
            while (hist.Count > 2 && now - hist[0].T > 0.1) hist.RemoveAt(0);
            if (_state == "press" && Math.Sqrt(Math.Pow(rx - touch.X0, 2) + Math.Pow(ry - touch.Y0, 2)) > Dp(8))
            {
                _state = "drag";
                _platform = null;
                if (_rng.NextDouble() < 0.25) Say("drag");
            }
            if (_state == "drag")
            {
                var vs = Forms.SystemInformation.VirtualScreen;
                _x = Clamp(rx - touch.Ox, vs.Left - _w * 0.3, vs.Right - _w * 0.7);
                _y = Clamp(ry - touch.Oy, vs.Top, vs.Bottom - _h * 0.5);
                var (t0, x0, _) = hist[0];
                if (now - t0 > 0.005) touch.Vx = (rx - x0) / (now - t0);
                _bounds = null;
                Render(now);
            }
            else if (_state == "pet")
            {
                double dx = rx - touch.Anchor;
                if (Math.Abs(dx) > Dp(10))
                {
                    int dir = dx > 0 ? 1 : -1;
                    if (touch.Dir != 0 && dir != touch.Dir) SpawnParticle("heart");
                    touch.Dir = dir;
                    touch.Anchor = rx;
                }
            }
        }

        void OnUp(bool cancelled)
        {
            var touch = _touch;
            if (touch == null) return;
            _touch = null;
            double now = Now;
            _lastInteraction = now;
            string state = _state;
            if (state == "drag") ReleaseDrag(now, touch, cancelled);
            else if (state == "pet") EndPet(now);
            else if (state == "press")
            {
                _state = "idle";
                if (!cancelled) Tap(now);
            }
            Schedule(0);
        }

        void ReleaseDrag(double now, Touch touch, bool cancelled)
        {
            double vx = 0, vy = 0;
            var hist = touch.Hist;
            if (!cancelled && hist.Count >= 2)
            {
                var (t0, x0, y0) = hist[0];
                var (t1, x1, y1) = hist[^1];
                if (t1 - t0 > 0.005 && now - t1 < 0.08)
                {
                    vx = (x1 - x0) / (t1 - t0);
                    vy = (y1 - y0) / (t1 - t0);
                }
            }
            double limit = Dp(6000);
            vx = Clamp(vx, -limit, limit);
            vy = Clamp(vy, -limit, limit);
            double speed = Math.Sqrt(vx * vx + vy * vy);
            _bounds = null;
            var b = Bounds(now);
            if (speed <= Dp(700) && _y <= b.MinY + _unit * 2)
            {
                StartHang(now, b.MinY);
            }
            else if (speed > Dp(700) || _cfg.Gravity)
            {
                _state = "fly";
                _vx = vx;
                _vy = vy;
                _launchT = now;
                double power = speed / Math.Max(1.0, Dp(1));
                string label = power > 4500 ? "сильнейший бросок" : power > 2600 ? "сильный бросок"
                    : power > 1200 ? "средний бросок" : "слабый бросок";
                if (speed > Dp(700)) Mem("throw", label, bond: power > 2600 ? -0.4 : 0.0);
                else Mem("drag");
                if (speed > Dp(2600)) Say("throw", ctx: new() { ["speed"] = label });
                else if (speed > Dp(1200) && AiReady() && _rng.NextDouble() < 0.5) Say("throw", ctx: new() { ["speed"] = label });
            }
            else
            {
                _state = "idle";
                _squashUntil = now + 0.12;
                _nextAction = now + U(2.0, 5.0);
                SavePosition();
                Mem("drag");
                if (_rng.NextDouble() < 0.5) Say("drop", ctx: new() { ["surface"] = "на то место, куда отпустили" });
            }
        }

        void Tap(double now)
        {
            if (now - _lastTap < 0.32)
            {
                _lastTap = 0;
                Hop(Dp(720));
                SetExpr("happy", 1.3);
                for (int i = 0; i < 3; i++) SpawnParticle("spark");
                Mem("jump", bond: 0.3);
                if (_rng.NextDouble() < 0.4) Say("jump");
                return;
            }
            _lastTap = now;
            _recentTaps = _recentTaps.Where(t => now - t < 2.5).Append(now).ToList();
            if (_recentTaps.Count >= 6)
            {
                _recentTaps.Clear();
                SetExpr("angry", 2.5);
                Mem("annoyed", bond: -1.0);
                Say("annoyed", force: true);
                return;
            }
            _squashUntil = now + 0.12;
            SetExpr("surprised", 0.45);
            Hop(Dp(330));
            Mem("tap", bond: 0.1);
            if (_rng.NextDouble() < 0.35) Say("tap");
        }

        void StartPet(double now)
        {
            _state = "pet";
            _nextHeart = now + 0.1;
        }

        void EndPet(double now)
        {
            _state = "idle";
            SetExpr("happy", 2.2);
            _nextAction = now + U(3.0, 6.0);
            _cfg.PetsCount++;
            Mem("pet", bond: 0.8);
            if (_rng.NextDouble() < 0.6) Say("pet");
        }

        void WakeForEvent(double now)
        {
            _lastInteraction = now;
            if (_state == "peek") EndPeek(now);
            else if (_state == "sleep")
            {
                _state = "idle";
                SetExpr("surprised", 0.9);
            }
        }

        // ---------- музыка и программы ----------

        void PollMedia(double now)
        {
            if (now < _mediaCheck) return;
            _mediaCheck = now + 0.5;
            string kind = null, track = null;
            if (_cfg.Dance && _media.Playing)
            {
                kind = "music";
                track = _media.Track;
            }
            if (kind == _mediaKind && (kind == null || track == _mediaTrack)) return;
            string previous = _mediaKind, prevTrack = _mediaTrack;
            _mediaKind = kind;
            _mediaTrack = track;
            _lastInteraction = now;
            if (kind == null)
            {
                if (previous == "music" && _state == "idle" && (AiReady() || _rng.NextDouble() < 0.5))
                    Say("music_end", ctx: new() { ["track"] = prevTrack });
                _nextAction = now + U(2.0, 4.0);
                return;
            }
            _nextNote = now + 0.3;
            if (track != null) RememberTrack(track);
            if (_state == "peek") EndPeek(now);
            else if (_state == "sleep")
            {
                _state = "idle";
                SetExpr("surprised", 0.8);
            }
            if (_state is not ("idle" or "walk")) return;
            var ctx = track != null ? new Dictionary<string, string> { ["track"] = track } : null;
            if (previous == null) Say("music", ctx: ctx);
            else if (AiReady()) Say("music", ctx: ctx);
        }

        void PollApp(double now)
        {
            var (key, name, title) = Desktop.Foreground(_pid);
            if (key == _appKey) return;
            _appKey = key;
            if (key == null || !_cfg.AppEvents) return;
            int visits = 1;
            if (_cfg.BrainEnabled)
            {
                try
                {
                    visits = _brain.AppVisit(key, name, "программа");
                    Mem("chat_open", name);
                }
                catch (Exception e) { Log.Write("brain app: " + e.Message); }
            }
            if (now < _nextAppComment || _state is not ("idle" or "walk") || _rng.NextDouble() > 0.35) return;
            _nextAppComment = now + 40.0;
            var ctx = new Dictionary<string, string> { ["name"] = name, ["visits"] = visits.ToString() };
            if (_cfg.AiTitles && !string.IsNullOrWhiteSpace(title)) ctx["title"] = title;
            Say(visits > 1 ? "app_again" : "app_new", name: name, ctx: ctx);
        }

        // ---------- иконки с панели задач и кучки ----------

        int SnackSize() => Math.Max(18, (int)(_unit * 5));

        public void Feed()
        {
            if (!_running) Start(false);
            if (!_posReady) return;
            double now = Now;
            if (_snacks.Count >= C.MaxSnacks) { ShowBubble("Я столько не съем!"); return; }
            if (_state == "sleep")
            {
                _state = "idle";
                SetExpr("surprised", 0.8);
            }
            _lastInteraction = now;
            _nextAction = Math.Min(_nextAction, now + 1.0);
            if (SpawnSnack(now) == null) return;
            Mem("feed", bond: 0.3);
            Say("feed");
            Schedule(0);
        }

        /// <summary>Иногда Clawd сам тащит иконку с панели задач.</summary>
        void MaybeStealIcon(double now)
        {
            if (!_cfg.EatIcons || now < _nextSteal) return;
            _nextSteal = now + U(C.StealGapMin, C.StealGapMax);
            if (_snacks.Count >= C.MaxSnacks || _touch != null || _hopActive) return;
            if (_state is not ("idle" or "walk") || _mediaKind != null) return;
            if (_bounds == null) return;
            var b = _bounds.Value;
            // «стащить с панели задач» можно, только стоя на ней
            if (Math.Abs(_y - b.MaxY) > _unit * 2) return;
            var snack = SpawnSnack(now);
            if (snack == null) return;
            Mem("steal", snack.Name, bond: 0.1);
            Say("steal", ctx: snack.Name != null ? new() { ["name"] = snack.Name } : null);
        }

        /// <summary>Иконка выезжает из панели задач рядом с питомцем.</summary>
        Snack SpawnSnack(double now)
        {
            var b = Bounds(now);
            bool grounded = _state is "idle" or "walk" or "sleep";
            double feet = grounded ? _y + _unit * 12 : b.MaxY + _unit * 12;
            double lo = b.ScrL, hi = b.ScrR;
            if (grounded && _platform != null && _platformSpan != null)
            {
                lo = Math.Max(lo, _platformSpan.Value.X0);
                hi = Math.Min(hi, _platformSpan.Value.X1);
            }
            int size = SnackSize();
            var icon = IconBank.Take();
            string name = null;
            BitmapSource bmp;
            if (icon != null)
            {
                bmp = Sprites.IconTile(icon.Bmp, size);
                name = icon.Name;
            }
            else
            {
                bmp = Sprites.FallbackIcon(size, _rng.Next(4));
            }
            int fw = bmp.PixelWidth, fh = bmp.PixelHeight;
            double cx = _x + _w / 2.0;
            int side = cx - lo > hi - cx ? -1 : 1;
            if (_rng.NextDouble() < 0.3) side = -side;
            double fx = Clamp(cx + side * U(Dp(120), Dp(320)), lo + fw, Math.Max(lo + fw, hi - fw));
            double restY = feet - fh;
            var win = new SpriteWindow(bmp, _scale, (int)Dp(3), (int)Dp(3), (int)Dp(3))
            {
                X = fx - fw / 2.0,
                Y = feet - fh * 0.15, // почти целиком спрятана в панели задач
            };
            var snack = new Snack { Win = win, Born = now, Name = name };
            win.Clicked = () => { };
            win.Show();
            win.SetLook(0.85, 0, 0);
            win.Animate(fx - fw / 2.0, restY, 1.0, 0, 1, 0.42, "overshoot", null);
            _snacks.Add(snack);
            _nextSteal = now + U(C.StealGapMin, C.StealGapMax);
            return snack;
        }

        bool GoForSnack(double now, double minX, double maxX)
        {
            Snack best = null;
            double bestDist = double.MaxValue;
            double feet = _y + _unit * 12;
            foreach (var f in _snacks)
            {
                if (f.Eating || Math.Abs(f.Win.Bottom - feet) > _unit * 4) continue;
                double d = Math.Abs(f.Win.CenterX - (_x + _w / 2.0));
                if (d < bestDist) { bestDist = d; best = f; }
            }
            if (best == null) return false;
            double fcx = best.Win.CenterX;
            bool fromLeft = _x + _w / 2.0 < fcx;
            double target = fcx - _w / 2.0 + (fromLeft ? -_w * 0.5 : _w * 0.5);
            var (lo, hi) = WalkRange(minX, maxX);
            target = Clamp(target, lo, hi);
            if (Math.Abs(target - _x) > Dp(6))
            {
                _walkTarget = target;
                _state = "walk";
                _goSnack = true;
                return true;
            }
            if (Math.Abs(fcx - (_x + _w / 2.0)) > _w * 1.2) return false; // не дотянуться
            EatSnack(now, best);
            return true;
        }

        (double X, double Y) MouthXY() => (_x + _w / 2.0, _y + _hopOff + _unit * 7);

        void EatSnack(double now, Snack snack)
        {
            snack.Eating = true;
            var (mx, my) = MouthXY();
            _look = snack.Win.CenterX > mx ? 1 : -1;
            _mouthOpenUntil = now + 0.7;
            _chewUntil = 0;
            _nextAction = now + 3.0;
            var w = snack.Win;
            w.Animate(mx - w.ImgW / 2.0, my - w.ImgH / 2.0, 0.05, Pick(new[] { -1, 1 }) * U(120, 300), 1, 0.56, "accel", () =>
            {
                _snacks.Remove(snack);
                w.Close();
                OnSwallowed(snack.Name);
            });
        }

        void OnSwallowed(string name)
        {
            double now = Now;
            _mouthOpenUntil = 0;
            _chewUntil = now + 1.4;
            _squashUntil = now + 0.12;
            SetExpr("happy", 1.6);
            _cfg.EatenCount++;
            Mem("eat", name, bond: 0.2);
            for (int i = 0; i < 2; i++) SpawnParticle("spark");
            if (_rng.NextDouble() < 0.7) Say("eat", ctx: name != null ? new() { ["name"] = name } : null);
            if (_cfg.Poop)
            {
                _cfg.PoopMeter++;
                if (_cfg.PoopMeter >= _poopNeed)
                {
                    _cfg.PoopMeter = 0;
                    _poopNeed = _rng.Next(C.PoopEvery.Item1, C.PoopEvery.Item2 + 1);
                    _poopDue = now + U(5.0, 10.0);
                }
            }
            _cfg.Save();
            Schedule(0);
        }

        void PruneSnacks(double now)
        {
            foreach (var f in _snacks.ToList())
            {
                if (f.Eating || now - f.Born < 180) continue;
                f.Eating = true;
                f.Win.Animate(f.Win.X, f.Win.Y, 0.6, 0, 0, 0.4, "linear", () => { _snacks.Remove(f); f.Win.Close(); });
            }
        }

        void DoPoop(double now, double minX, double maxX)
        {
            _poopDue = 0;
            if (!_cfg.Poop || _poops.Count >= C.MaxPoops) return;
            int side = _look != 0 ? -_look : Pick(new[] { -1, 1 });
            var bmp = ParticleBmp("poop");
            int bw = bmp.PixelWidth, bh = bmp.PixelHeight;
            double feet = _y + _unit * 12;
            var b = Bounds(now);
            double px = Clamp(_x + _w / 2.0 + side * _w * 0.42 - bw / 2.0, b.ScrL, b.ScrR - bw);
            var win = new SpriteWindow(bmp, _scale, (int)Dp(8), (int)Dp(50), (int)Dp(4)) { X = px, Y = feet - bh };
            win.Clicked = () => CleanPoop(win);
            win.Show();
            win.SetLook(0.1, 0, 1);
            win.Animate(win.X, win.Y, 1.0, 0, 1, 0.32, "overshoot", null);
            _poops.Add(win);
            _squashUntil = now + 0.2;
            SetExpr("happy", 1.8);
            Mem("poop");
            Say("poop");
            _look = -side;
            double target = Clamp(_x - side * U(Dp(40), Dp(80)), minX, maxX);
            if (Math.Abs(target - _x) > Dp(12))
            {
                _walkTarget = target;
                _state = "walk";
            }
            _nextAction = now + U(3.0, 6.0);
        }

        void CleanPoop(SpriteWindow win)
        {
            if (!_poops.Remove(win)) return;
            win.Clicked = null;
            win.Burst(ParticleBmp("spark"), 3, _rng);
            win.Animate(win.X, win.Y, 0.0, 90, 0, 0.26, "linear", null);
            var close = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            close.Tick += (_, _) => { close.Stop(); win.Close(); };
            close.Start();
            _cfg.PoopsCleaned++;
            _cfg.Save();
            Mem("clean", bond: 0.6);
            if (_state is "idle" or "walk")
            {
                SetExpr("happy", 1.5);
                if (_rng.NextDouble() < 0.7) Say("clean");
                Schedule(0);
            }
        }

        // ---------- эффекты ----------

        void SetExpr(string expr, double duration)
        {
            _expr = expr;
            _exprUntil = Now + duration;
        }

        void Hop(double strength)
        {
            if (_state is not ("idle" or "walk")) return;
            int j = Math.Max(0, Math.Min(C.JumpScale.Length - 1, _cfg.Jump));
            _hopActive = true;
            _hopVy = -strength * C.JumpScale[j];
            if (_hopOff > 0) _hopOff = 0;
        }

        void SpawnParticle(string kind)
        {
            if (!Win.IsVisible || Win.ParticleCount >= C.MaxParticles) return;
            var bmp = ParticleBmp(kind);
            int bw = bmp.PixelWidth, bh = bmp.PixelHeight;
            double headY = _y + _hopOff + _unit * 2 - bh;
            double sx, sy, drift;
            if (kind == "zzz")
            {
                sx = _x + _w * 0.72;
                sy = headY + _unit * 2;
                drift = Dp(18);
            }
            else
            {
                sx = _x + _w / 2.0 - bw / 2.0 + U(-_w * 0.25, _w * 0.25);
                sy = headY;
                drift = U(-Dp(16), Dp(16));
            }
            double lx = sx - _x + Win.ML;
            double ly = sy - (_y + _hopOff) + Win.MT;
            Win.AddParticle(bmp, lx, ly, drift, Dp(U(40, 58)), U(-15, 15), kind == "zzz" ? 1.6 : 1.1);
        }

        // ---------- пузырь ----------

        void ShowBubble(string text)
        {
            Win.ShowBubble(text);
            _bubbleUntil = Now + 2.0 + text.Length * 0.05;
            Schedule(0);
        }

        void UpdateBubble(double now, double scrL, double scrR, double minY)
        {
            if (!Win.BubbleVisible) return;
            if (now >= _bubbleUntil)
            {
                Win.HideBubble();
                return;
            }
            var (bw, bh) = Win.BubbleSize();
            if (bw <= 0) return;
            double margin = Dp(4);
            double bx = Clamp(_x + _w / 2.0 - bw / 2.0, scrL + margin, scrR - bw - margin);
            double by = _y + _hopOff + _unit - bh - margin;
            if (by < minY) by = _y + _hopOff + _h + margin;
            double lx = bx - Win.PxX, ly = by - Win.PxY;
            lx = Clamp(lx, 0, Win.PxW - bw);
            ly = Clamp(ly, 0, Win.PxH - bh);
            Win.SetBubbleLocal(lx, ly);
        }
    }
}
