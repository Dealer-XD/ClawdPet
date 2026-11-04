using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ClawdPet
{
    /// <summary>Порт логики ClawdPetPlugin: состояния, физика, реакции, ИИ, память.</summary>
    public partial class Pet
    {
        class Touch
        {
            public double X0, Y0, T0, Ox, Oy, Anchor, Vx;
            public int Dir;
            public List<(double T, double X, double Y)> Hist = new();
        }

        class Food
        {
            public SpriteWindow Win;
            public double Born;
            public bool Eating;
        }

        readonly Config _cfg;
        readonly Brain _brain;
        readonly MediaWatcher _media = new();
        readonly Random _rng = new();
        readonly Stopwatch _clock = Stopwatch.StartNew();
        readonly int _pid = Environment.ProcessId;
        readonly DispatcherTimer _timer;
        readonly DispatcherTimer _brainTimer;
        public readonly PetWindow Win;
        public Action OpenMenu;
        public Action PaletteChanged;

        double Now => _clock.Elapsed.TotalSeconds;
        double U(double a, double b) => a + _rng.NextDouble() * (b - a);
        T Pick<T>(IList<T> items) => items[_rng.Next(items.Count)];
        static double Clamp(double v, double lo, double hi) => hi < lo ? lo : (v < lo ? lo : (v > hi ? hi : v));

        double _scale = 1.0;
        int _unit, _w, _h;
        double Dp(double v) => v * _scale;

        bool _running, _hiddenFs;
        string _state = "idle";
        double _x, _y, _vx, _vy;
        bool _posReady;
        double _lastTick, _lastInteraction, _nextAction, _nextBlink, _blinkUntil;
        int _look;
        string _expr;
        double _exprUntil, _squashUntil, _waveUntil;
        bool _hopActive;
        double _hopOff, _hopVy, _walkTarget;
        Touch _touch;
        double _lastTap;
        List<double> _recentTaps = new();
        double _nextHeart, _nextZ;
        double _bubbleUntil;
        string _lastPhrase;
        (double MinX, double MaxX, double MinY, double MaxY, double ScrL, double ScrR)? _bounds;
        double _boundsTime, _slowCheck, _topCheck;
        bool _greetPending;
        double _mouthOpenUntil, _chewUntil;
        string _mediaKind, _mediaTrack;
        double _mediaCheck, _nextNote;
        string _appKey;
        double _nextAppComment;
        double _shakePending;
        bool _shakeDizzy;
        double _launchT = -100;
        Platform _platform;
        List<Platform> _platforms = new();
        double _platformScan, _followScan;
        bool _platformMoving, _walkIn;
        (double X0, double X1)? _platformSpan;
        int _shakeSign;
        List<double> _shakeHits = new();
        double _shakeSpeed, _rideDist, _nextRideSay;
        bool _rideLogged;
        double _hangStart, _hangUntil;
        int _peekSide, _peekPending;
        string _peekPhase = "hide";
        double _peekPhaseUntil, _peekEnd, _nextPeek;
        bool _peekMoving;
        double _poopDue;
        int _poopNeed;
        bool _goFood;
        bool _userAway;
        readonly List<SpriteWindow> _poops = new();
        readonly List<Food> _foods = new();

        readonly Dictionary<FrameKey, BitmapSource> _frames = new();
        readonly Dictionary<string, BitmapSource> _particleBmps = new();
        FrameKey? _frameKey;

        // ИИ
        volatile bool _aiBusy;
        double _aiNext, _aiIdleNext, _aiErrNext;
        readonly double[] _aiBo = { 0.0, 0.0 };
        int _sayGen;
        List<string> _aiHistory = new();

        public Pet(Config cfg)
        {
            _cfg = cfg;
            _brain = new Brain(System.IO.Path.Combine(Config.Dir, C.BrainFile));
            Win = new PetWindow();
            Win.PetDown += _ => OnDown();
            Win.PetMove += OnMove;
            Win.PetUp += OnUp;
            Win.PetMenu += () => OpenMenu?.Invoke();
            Win.ScaleChanged += s => { _scale = s; RebuildSprites(); };
            _timer = new DispatcherTimer(DispatcherPriority.Render);
            _timer.Tick += (_, _) => Tick();
            _brainTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(C.BrainSaveDelayMs) };
            _brainTimer.Tick += (_, _) => { _brainTimer.Stop(); BrainFlush(); };
            ResetState();
            BrainInit();
        }

        // ---------- жизненный цикл ----------

        public void Start(bool greet)
        {
            if (!_cfg.Show) return;
            if (!Win.IsVisible)
            {
                Win.Show();
                _scale = Win.Scale;
                UpdateMetrics();
                Win.Layout(_w, _h);
            }
            foreach (var p in _poops) p.Show();
            foreach (var f in _foods) f.Win.Show();
            if (_cfg.Dance) _media.Start();
            if (!_running)
            {
                _running = true;
                _lastTick = Now;
                if (greet) _greetPending = true;
                Schedule(16);
            }
        }

        public void Stop()
        {
            _running = false;
            _timer.Stop();
            _media.Stop();
            Win.HideBubble();
            Win.Hide();
            foreach (var p in _poops) p.Hide();
            foreach (var f in _foods) f.Win.Hide();
        }

        public void Shutdown()
        {
            SavePosition();
            BrainFlush();
            _cfg.Save();
            _running = false;
            _timer.Stop();
            _media.Stop();
            foreach (var p in _poops) p.Close();
            foreach (var f in _foods) f.Win.Close();
            Win.Close();
        }

        public void ConfigChanged(bool visuals)
        {
            _cfg.Save();
            if (visuals)
            {
                RebuildSprites();
                PaletteChanged?.Invoke();
            }
            if (_cfg.Show && !_running) Start(false);
            else if (!_cfg.Show && _running) Stop();
            if (_running)
            {
                if (_cfg.Dance) _media.Start(); else _media.Stop();
                Schedule(0);
            }
        }

        void Schedule(double delayMs)
        {
            if (!_running) return;
            _timer.Stop();
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, delayMs));
            _timer.Start();
        }

        void Tick()
        {
            _timer.Stop();
            if (!_running) return;
            try { Step(); }
            catch (Exception e) { Log.Write("tick: " + e); }
            if (_running) Schedule(NextDelay());
        }

        double NextDelay()
        {
            if (_hiddenFs) return 1000;
            double now = Now;
            if (_state is "press" or "drag" or "pet" or "fly" or "walk" || _hopActive || _walkIn || _platformMoving
                || _shakePending > 0 || now < _squashUntil || (_expr == "dizzy" && now < _exprUntil))
                return 16;
            if (_state is "hang" or "peek") return 33;
            if (_state == "sleep") return 250;
            if (_mediaKind != null && _state == "idle") return 33;
            return 80;
        }

        // ---------- спрайты ----------

        void UpdateMetrics()
        {
            int size = (int)Clamp(_cfg.Size, 0, C.SizeDp.Length - 1);
            _unit = Math.Max(2, (int)Math.Round(C.SizeDp[size] * _scale));
            _w = C.GridW * _unit;
            _h = C.GridH * _unit;
        }

        void RebuildSprites()
        {
            int oldW = _w, oldH = _h;
            _frames.Clear();
            _particleBmps.Clear();
            _frameKey = null;
            UpdateMetrics();
            _x += (oldW - _w) / 2.0;
            _y += oldH - _h;
            _bounds = null;
            if (Win.Hwnd != IntPtr.Zero) Win.Layout(_w, _h);
            Hop(Dp(300));
            Schedule(16);
        }

        (string Name, uint Body, uint Eye) Palette => C.Palettes[(int)Clamp(_cfg.Color, 0, C.Palettes.Length - 1)];
        public System.Drawing.Icon MakeTrayIcon() => Sprites.TrayIcon(Palette.Body, Palette.Eye);

        BitmapSource Frame(FrameKey key)
        {
            if (_frames.TryGetValue(key, out var bmp)) return bmp;
            if (_frames.Count >= C.MaxFrameCache) _frames.Remove(_frames.Keys.First());
            var pal = Palette;
            bmp = Sprites.RenderFrame(key, _unit, pal.Body, pal.Eye).ToBitmap();
            _frames[key] = bmp;
            return bmp;
        }

        BitmapSource ParticleBmp(string kind)
        {
            if (_particleBmps.TryGetValue(kind, out var bmp)) return bmp;
            int s = kind is "poop" or "cookie" ? Math.Max(2, (int)(_unit * 0.9)) : Math.Max(2, (int)(_unit * 0.75));
            bmp = Sprites.Pattern(kind, s);
            _particleBmps[kind] = bmp;
            return bmp;
        }

        void ResetState()
        {
            double now = Now;
            _state = "idle";
            _x = _y = _vx = _vy = 0;
            _posReady = false;
            _lastTick = now;
            _lastInteraction = now;
            _nextAction = now + 4.0;
            _nextBlink = now + 2.0;
            _nextAppComment = now + 8.0;
            _nextPeek = now + C.PeekAfter;
            _poopNeed = _rng.Next(C.PoopEvery.Item1, C.PoopEvery.Item2 + 1);
        }

        // ---------- главный шаг ----------

        (double MinX, double MaxX, double MinY, double MaxY, double ScrL, double ScrR) Bounds(double now)
        {
            if (_bounds != null && (now - _boundsTime < 0.4 || _state == "peek")) return _bounds.Value;
            var center = new System.Drawing.Point((int)(_x + _w / 2.0), (int)(_y + _h / 2.0));
            var wa = Forms.Screen.FromPoint(center).WorkingArea;
            _bounds = (wa.Left, Math.Max(wa.Left, wa.Right - _w), wa.Top, Math.Max(wa.Top, wa.Bottom - _h), wa.Left, wa.Right);
            _boundsTime = now;
            return _bounds.Value;
        }

        void Step()
        {
            double now = Now;
            double dt = Clamp(now - _lastTick, 0.0, 0.05);
            _lastTick = now;

            if (now >= _slowCheck)
            {
                _slowCheck = now + 1.0;
                SlowChecks(now);
            }
            if (_hiddenFs) return;
            if (now >= _topCheck)
            {
                _topCheck = now + 2.0;
                Win.KeepOnTop();
                foreach (var p in _poops) p.KeepOnTop();
                foreach (var f in _foods) f.Win.KeepOnTop();
            }

            if (!_posReady)
            {
                var wa = Forms.Screen.PrimaryScreen.WorkingArea;
                _x = wa.Left + wa.Width / 2.0;
                _y = wa.Top + wa.Height / 2.0;
                _bounds = null;
                var b0 = Bounds(now);
                RestorePosition(b0.MinX, b0.MaxX, b0.MinY, b0.MaxY);
                _bounds = null;
                _posReady = true;
                if (_greetPending)
                {
                    _greetPending = false;
                    Hop(Dp(420));
                    Say("hello");
                }
            }
            PollMedia(now);

            var (minX, maxX, minY, maxY, scrL, scrR) = Bounds(now);
            bool gravity = _cfg.Gravity;
            string state = _state;

            if (state == "press" && _touch != null && now - _touch.T0 >= 0.4)
            {
                StartPet(now);
                state = _state;
            }
            if (_shakePending > 0 && state is not ("press" or "drag" or "pet"))
            {
                ApplyShake(now, gravity);
                state = _state;
            }

            if (state == "pet")
            {
                if (now >= _nextHeart)
                {
                    _nextHeart = now + 0.45;
                    SpawnParticle("heart");
                }
            }
            else if (state == "fly") StepFly(dt, now, gravity, minX, maxX, minY, maxY);
            else if (state == "hang") StepHang(now, gravity, minX, maxX, minY);
            else if (state == "peek") StepPeek(dt, now, scrL, scrR);
            else if (state is "idle" or "walk" or "sleep") StepGround(dt, now, gravity, minX, maxX, minY, maxY);

            if (_hopActive)
            {
                _hopOff += _hopVy * dt;
                _hopVy += Dp(2600) * dt;
                if (_hopOff >= 0.0)
                {
                    _hopOff = 0.0;
                    _hopVy = 0.0;
                    _hopActive = false;
                    _squashUntil = now + 0.1;
                }
            }
            Render(now);
            UpdateBubble(now, scrL, scrR, minY);
        }

        void SlowChecks(double now)
        {
            bool fs = _cfg.HideFullscreen && Desktop.ForegroundIsFullscreen(_pid);
            if (fs != _hiddenFs)
            {
                _hiddenFs = fs;
                if (fs)
                {
                    Win.Hide();
                    foreach (var p in _poops) p.Hide();
                    foreach (var f in _foods) f.Win.Hide();
                }
                else
                {
                    Win.Show();
                    foreach (var p in _poops) p.Show();
                    foreach (var f in _foods) f.Win.Show();
                    _lastTick = now;
                }
            }

            double idle = Native.IdleSeconds();
            if (idle > C.BrainSessionGap) _userAway = true;
            else if (_userAway && idle < 3)
            {
                _userAway = false;
                OnUserBack(now);
            }

            if (_hiddenFs || !_posReady) return;
            PruneFood(now);
            PollApp(now);
        }

        void OnUserBack(double now)
        {
            double? away = null;
            if (_cfg.BrainEnabled)
            {
                try { away = _brain.SessionStart(); BrainDirty(); }
                catch (Exception e) { Log.Write("session: " + e.Message); }
            }
            WakeForEvent(now);
            if (_state is "idle" or "walk")
            {
                Hop(Dp(420));
                _waveUntil = now + 1.6;
                Say("back", ctx: away != null ? new() { ["away"] = Brain.RelTime(away.Value).Replace(" назад", "") } : null);
            }
        }

        // ---------- земля, ходьба ----------

        void StepGround(double dt, double now, bool gravity, double minX, double maxX, double minY, double maxY)
        {
            _walkIn = false;
            if (_state != "walk")
            {
                if (_x < minX - 0.5 || _x > maxX + 0.5)
                {
                    double step = Dp(60) * dt;
                    if (_x < minX) { _x = Math.Min(minX, _x + step); _look = 1; }
                    else { _x = Math.Max(maxX, _x - step); _look = -1; }
                    _walkIn = true;
                }
                else _x = Clamp(_x, minX, maxX);
            }

            _platformMoving = false;
            if (gravity)
            {
                if (_platform != null)
                {
                    if (!FollowPlatform(now, minY, maxY))
                    {
                        Fall(_state == "walk" ? Dp(55) * _look : 0.0);
                        return;
                    }
                }
                else if (_y < maxY - 0.5 && !_hopActive)
                {
                    Fall(0.0);
                    return;
                }
                else _y = Math.Min(_y, maxY);
            }
            else _y = Clamp(_y, minY, maxY);

            if (_state == "walk")
            {
                int dir = _walkTarget > _x ? 1 : -1;
                _look = dir;
                _x += dir * Dp(55) * dt;
                bool reached = (dir > 0 && _x >= _walkTarget) || (dir < 0 && _x <= _walkTarget);
                bool hitEdge = (dir < 0 && _x <= minX) || (dir > 0 && _x >= maxX);
                if (reached || hitEdge)
                {
                    _x = Clamp(reached ? _walkTarget : _x, minX, maxX);
                    _state = "idle";
                    _nextAction = now + (_goFood ? 0.3 : U(2.5, 6.0));
                    _goFood = false;
                    int side = _peekPending;
                    _peekPending = 0;
                    double edge = Dp(4);
                    if (side != 0 && ((side < 0 && _x <= minX + edge) || (side > 0 && _x >= maxX - edge)))
                    {
                        StartPeek(now, side);
                        return;
                    }
                    SavePosition();
                }
                return;
            }

            if (_state == "sleep")
            {
                if (now >= _nextZ)
                {
                    _nextZ = now + 1.6;
                    SpawnParticle("zzz");
                }
                return;
            }

            if (now >= _nextBlink)
            {
                _blinkUntil = now + 0.13;
                _nextBlink = now + (_rng.NextDouble() < 0.15 ? 0.35 : U(2.0, 5.5));
            }

            int timeout = C.SleepSeconds[(int)Clamp(_cfg.Sleep, 0, C.SleepSeconds.Length - 1)];
            if (timeout > 0 && now - _lastInteraction > timeout && !_hopActive && _mediaKind == null && !_walkIn
                && _foods.Count == 0)
            {
                _state = "sleep";
                _look = 0;
                _nextZ = now + 0.6;
                Win.HideBubble();
                return;
            }

            if (_poopDue > 0 && now >= _poopDue && !_hopActive && !_walkIn)
            {
                DoPoop(now, minX, maxX);
                return;
            }

            if (_foods.Count > 0 && !_hopActive && !_walkIn && _state == "idle" && now >= _nextAction - 3.0
                && GoForFood(now, minX, maxX))
                return;

            if (_mediaKind != null)
            {
                if (now >= _nextNote)
                {
                    _nextNote = now + 0.8;
                    SpawnParticle("note");
                }
                return;
            }

            if (now < _nextAction || _hopActive || _walkIn) return;
            _nextAction = now + U(3.5, 9.0);
            double roll = _rng.NextDouble();
            if (!_cfg.Wander)
            {
                if (roll < 0.5) _look = Pick(new[] { -1, 0, 1 });
                return;
            }
            if (_platform == null && now >= _nextPeek && now - _lastInteraction > C.PeekAfter && roll < 0.3)
            {
                int side = _x - minX < maxX - _x ? -1 : 1;
                _walkTarget = side < 0 ? minX : maxX;
                _peekPending = side;
                _state = "walk";
                return;
            }
            if (roll < 0.38)
            {
                double distance = U(Dp(60), Dp(320)) * Pick(new[] { -1, 1 });
                var (lo, hi) = WalkRange(minX, maxX);
                double target = Clamp(_x + distance, lo, hi);
                if (Math.Abs(target - _x) > Dp(12))
                {
                    _walkTarget = target;
                    _state = "walk";
                }
            }
            else if (roll < 0.58) _look = Pick(new[] { -1, 0, 1 });
            else if (roll < 0.72) Say("idle");
            else if (roll < 0.84) Hop(Dp(360));
            else _waveUntil = now + 1.6;
        }

        (double Lo, double Hi) WalkRange(double minX, double maxX)
        {
            double lo = minX, hi = maxX;
            if (_platform != null && _platformSpan != null)
            {
                lo = Math.Max(lo, _platformSpan.Value.X0 - _unit * 4);
                hi = Math.Min(hi, _platformSpan.Value.X1 - _unit * 14);
            }
            return (lo, hi);
        }

        void Fall(double vx)
        {
            _state = "fly";
            _platform = null;
            _platforms = new();
            _platformScan = 0;
            _vx = vx;
            _vy = 0;
        }

        bool FeetOn(double x0, double x1)
        {
            double f0 = _x + _unit * 4, f1 = _x + _unit * 14;
            return Math.Min(f1, x1) - Math.Max(f0, x0) >= _unit * 2;
        }

        List<Platform> Scan(double minY, double maxY) =>
            _cfg.RideWindows
                ? Desktop.ScanPlatforms(_pid, minY - _unit * 2 + _h, maxY + _h - 1)
                : new List<Platform>();

        bool FollowPlatform(double now, double minY, double maxY)
        {
            if (!_cfg.RideWindows) return false;
            if (now < _followScan) return true;
            _followScan = now + 0.04;
            var plats = Scan(minY, maxY);
            Platform best = null;
            double bestOverlap = -1;
            double dx = 0;
            foreach (var p in plats)
            {
                if (p.Hwnd != _platform.Hwnd) continue;
                double shift = p.Left - _platform.Left;
                double f0 = _x + shift + _unit * 4, f1 = _x + shift + _unit * 14;
                double overlap = Math.Min(f1, p.X1) - Math.Max(f0, p.X0);
                if (overlap > bestOverlap) { bestOverlap = overlap; best = p; dx = shift; }
            }
            if (best == null || bestOverlap < _unit * 2) return false;
            double y = best.Top - _h;
            if (y < minY - _unit * 2 || y > maxY) return false;
            best.Name = _platform.Name;
            if (Math.Abs(dx) > 0.5 && _state != "drag")
            {
                _x += dx;
                TrackRide(now, dx);
            }
            _platformMoving = Math.Abs(y - _y) > 0.5 || Math.Abs(dx) > 0.5;
            _platformSpan = (best.X0, best.X1);
            _platform = best;
            _y = y;
            return true;
        }

        void TrackRide(double now, double dx)
        {
            if (_cfg.ShakeWindows && Math.Abs(dx) >= Dp(4))
            {
                int sign = Math.Sign(dx);
                _shakeSpeed = Math.Max(_shakeSpeed * 0.9, Math.Abs(dx) / 0.04);
                if (_shakeSign != 0 && sign != _shakeSign)
                {
                    _shakeHits = _shakeHits.Where(t => now - t < C.ShakeWindow).ToList();
                    _shakeHits.Add(now);
                    if (_shakeHits.Count >= C.ShakeHits - 1)
                    {
                        _shakeHits.Clear();
                        _shakePending = Clamp(_shakeSpeed / Dp(120), 14, 40);
                    }
                }
                _shakeSign = sign;
            }
            _rideDist += Math.Abs(dx);
            if (_rideDist > Dp(200) && now >= _nextRideSay && _shakePending <= 0)
            {
                _nextRideSay = now + 25.0;
                _rideDist = 0;
                if (!_rideLogged) { _rideLogged = true; Mem("ride", _platform?.Name); }
                if (_rng.NextDouble() < 0.6) Say("ride", ctx: new() { ["name"] = _platform?.Name });
            }
        }

        Platform PlatformLanding(double now, double oldY, double minY, double maxY, out double speed)
        {
            speed = 0;
            if (now >= _platformScan)
            {
                _platformScan = now + 0.25;
                _platforms = Scan(minY, maxY);
            }
            double prevBottom = oldY + _h, newBottom = _y + _h;
            Platform best = null;
            foreach (var p in _platforms)
                if (prevBottom <= p.Top + 1 && newBottom >= p.Top && FeetOn(p.X0, p.X1))
                    if (best == null || p.Top < best.Top) best = p;
            if (best == null) return null;
            speed = _vy;
            _y = best.Top - _h;
            if (speed > Dp(380))
            {
                _vy = -speed * 0.42;
                _vx *= 0.75;
                _squashUntil = now + 0.1;
            }
            else
            {
                _platformSpan = (best.X0, best.X1);
                best.Name = Desktop.AppName(best.Hwnd);
                Land(now, best);
            }
            return best;
        }

        void StepFly(double dt, double now, bool gravity, double minX, double maxX, double minY, double maxY)
        {
            double oldY = _y;
            _x += _vx * dt;
            _y += _vy * dt;
            if (gravity)
            {
                _vy += Dp(2600) * dt;
                _vx *= Math.Exp(-0.35 * dt);
            }
            else
            {
                double fr = Math.Exp(-2.0 * dt);
                _vx *= fr;
                _vy *= fr;
            }

            double impact = 0;
            if (_x < minX) { _x = minX; impact = Math.Max(impact, Math.Abs(_vx)); _vx = Math.Abs(_vx) * 0.55; }
            else if (_x > maxX) { _x = maxX; impact = Math.Max(impact, Math.Abs(_vx)); _vx = -Math.Abs(_vx) * 0.55; }

            Platform landed = null;
            double landSpeed = 0;
            if (gravity && _vy > 0 && _y > oldY) landed = PlatformLanding(now, oldY, minY, maxY, out landSpeed);
            if (landed != null) impact = Math.Max(impact, landSpeed);
            else if (_y < minY)
            {
                if (_vy < -Dp(250) && !_shakeDizzy)
                {
                    StartHang(now, minY);
                    return;
                }
                _y = minY;
                impact = Math.Max(impact, Math.Abs(_vy));
                _vy = Math.Abs(_vy) * 0.55;
            }
            else if (_y > maxY)
            {
                _y = maxY;
                impact = Math.Max(impact, Math.Abs(_vy));
                if (gravity && _vy < Dp(380)) Land(now, null);
                else
                {
                    _vy = -Math.Abs(_vy) * (gravity ? 0.42 : 0.55);
                    _vx *= gravity ? 0.75 : 1.0;
                    _squashUntil = now + 0.1;
                }
            }

            if (impact > Dp(2300))
            {
                SetExpr("dizzy", 2.6);
                Say("dizzy");
            }
            if (_state == "fly" && !gravity && Math.Sqrt(_vx * _vx + _vy * _vy) < Dp(45)) Land(now, null);
        }

        void Land(double now, Platform platform)
        {
            _state = "idle";
            _vx = _vy = 0;
            _platform = platform;
            _platforms = new();
            _rideDist = 0;
            _rideLogged = false;
            _shakeHits.Clear();
            _shakeSign = 0;
            _shakeSpeed = 0;
            _squashUntil = now + 0.14;
            _nextAction = now + U(2.0, 5.0);
            bool launched = now - _launchT < 20.0;
            _launchT = -100;
            if (_shakeDizzy)
            {
                _shakeDizzy = false;
                SetExpr("dizzy", 3.0);
                Say("dizzy");
                if (_bounds != null)
                {
                    var b = _bounds.Value;
                    _walkTarget = Clamp(_x + U(Dp(30), Dp(70)) * Pick(new[] { -1, 1 }), b.MinX, b.MaxX);
                    if (Math.Abs(_walkTarget - _x) > Dp(12)) _state = "walk";
                }
            }
            else if (platform != null && _rng.NextDouble() < 0.6)
            {
                var name = platform.Name;
                Say("land_window", ctx: new()
                {
                    ["name"] = name,
                    ["surface"] = string.IsNullOrEmpty(name) ? "на верхний край окна" : $"на верхний край окна «{name}»",
                });
            }
            else if (launched && _rng.NextDouble() < 0.6)
                Say("drop", ctx: new() { ["surface"] = "на панель задач, в самом низу экрана" });
            SavePosition();
        }

        // ---------- висение и подглядывание ----------

        void StartHang(double now, double minY)
        {
            _state = "hang";
            _vx = _vy = 0;
            _platform = null;
            _y = minY - _unit * 2;
            _hangStart = now;
            _hangUntil = now + U(2.0, 3.5);
            Mem("hang");
            if (_rng.NextDouble() < 0.7) Say("hang");
        }

        void StepHang(double now, bool gravity, double minX, double maxX, double minY)
        {
            _x = Clamp(_x, minX, maxX);
            _y = minY - _unit * 2;
            if (now < _hangUntil) return;
            _state = "fly";
            _vx = 0;
            _vy = gravity ? Dp(150) : Dp(700);
            SetExpr("surprised", 0.8);
            if (_rng.NextDouble() < 0.6) Say("hang_fall");
        }

        double PeekX(double scrL, double scrR, int units) =>
            _peekSide > 0 ? scrR - units * _unit : scrL + units * _unit - _w;

        void StartPeek(double now, int side)
        {
            _state = "peek";
            _peekSide = side;
            _peekPhase = "hide";
            _peekPhaseUntil = now + U(3.0, 6.0);
            _peekEnd = now + U(30.0, 60.0);
            _look = side;
        }

        void StepPeek(double dt, double now, double scrL, double scrR)
        {
            string phase = _peekPhase;
            double target = PeekX(scrL, scrR, phase == "out" ? C.PeekOutUnits : C.PeekHiddenUnits);
            double step = Dp(50) * dt;
            _peekMoving = Math.Abs(target - _x) > 0.5;
            if (_peekMoving) _x = target > _x ? Math.Min(target, _x + step) : Math.Max(target, _x - step);
            _look = phase == "hide" && _peekMoving ? _peekSide : -_peekSide;
            if (now >= _nextBlink)
            {
                _blinkUntil = now + 0.13;
                _nextBlink = now + U(2.0, 5.5);
            }
            if (now < _peekPhaseUntil || _peekMoving) return;
            if (now >= _peekEnd)
            {
                EndPeek(now);
                return;
            }
            if (phase == "hide")
            {
                _peekPhase = "out";
                _peekPhaseUntil = now + U(1.5, 2.8);
                if (_rng.NextDouble() < 0.5) Say("peek");
            }
            else
            {
                _peekPhase = "hide";
                _peekPhaseUntil = now + U(3.0, 7.0);
            }
        }

        void EndPeek(double now)
        {
            int side = _peekSide;
            _peekSide = 0;
            _peekMoving = false;
            _nextPeek = now + U(60.0, 120.0);
            _state = "idle";
            if (_bounds == null || side == 0) return;
            var b = _bounds.Value;
            double inside = side < 0 ? b.MinX + Dp(30) : b.MaxX - Dp(30);
            _walkTarget = Clamp(inside, b.MinX, b.MaxX);
            _state = "walk";
            _nextAction = now + U(3.0, 6.0);
        }

        // ---------- тряска окна ----------

        void ApplyShake(double now, bool gravity)
        {
            double power = Math.Min(_shakePending, 40.0);
            _shakePending = 0;
            bool wasSleeping = _state == "sleep";
            if (_state == "peek") _peekSide = 0;
            _peekPending = 0;
            _platform = null;
            _platforms = new();
            _platformScan = 0;
            _hopActive = false;
            _hopOff = _hopVy = 0;
            _state = "fly";
            double speed = Dp(900 + power * 55);
            double angle = U(Math.PI * 1.1, Math.PI * 1.9); // в основном вверх
            _vx = Math.Cos(angle) * speed;
            _vy = Math.Sin(angle) * speed - (gravity ? Dp(500) : 0.0);
            _shakeDizzy = true;
            _lastInteraction = now;
            SetExpr("surprised", 1.0);
            Mem("shake", bond: -0.2);
            if (wasSleeping) Say("wake");
            else if (_rng.NextDouble() < 0.6) Say("shake");
        }

        // ---------- отрисовка ----------

        FrameKey Compose(double now)
        {
            string eyes = "open", arms = "normal", legs = "stand", mouth = null;
            int look = _look, squash = 0;
            bool blush = false;
            string state = _state;

            switch (state)
            {
                case "drag":
                case "fly":
                    eyes = "surprised"; arms = "up"; legs = "dangle";
                    break;
                case "pet":
                    eyes = "happy"; blush = true; look = 0;
                    squash = ((int)(now * 3)) % 2 == 1 ? 1 : 0;
                    break;
                case "sleep":
                    eyes = "sleep"; arms = "tuck"; legs = "tuck"; look = 0;
                    break;
                case "walk":
                    legs = ((int)(now * 7)) % 2 == 1 ? "walk1" : "walk2";
                    break;
                case "press":
                    squash = 1;
                    break;
                case "hang":
                    eyes = now - _hangStart < 0.5 ? "surprised" : "open";
                    arms = "up"; legs = "dangle";
                    look = ((int)(now * 1.5)) % 2 == 1 ? -1 : 1;
                    break;
                case "peek":
                    if (_peekMoving) legs = ((int)(now * 7)) % 2 == 1 ? "walk1" : "walk2";
                    else if (_peekPhase == "out" && ((int)(now * 3)) % 2 == 1) arms = _peekSide > 0 ? "wave1" : "wave2";
                    break;
            }

            if (_walkIn && state == "idle") legs = ((int)(now * 7)) % 2 == 1 ? "walk1" : "walk2";

            if (state == "idle" && _mediaKind != null && !_hopActive)
            {
                bool beat = ((int)(now * 4)) % 2 == 1;
                eyes = "happy";
                arms = beat ? "wave1" : "wave2";
                legs = beat ? "walk1" : "walk2";
                squash = beat ? 1 : 0;
                look = ((int)(now * 2)) % 2 == 1 ? -1 : 1;
            }

            if (state is ("idle" or "walk") && now < _waveUntil) arms = ((int)(now * 5)) % 2 == 1 ? "wave1" : "wave2";

            if (_hopActive && state is ("idle" or "walk"))
            {
                legs = "dangle";
                if (_hopVy < 0) { squash = -1; arms = "up"; }
            }

            if (now < _exprUntil && _expr != null && state != "sleep")
            {
                eyes = _expr;
                if (_expr == "happy") blush = true;
            }
            if (now < _squashUntil && squash == 0) squash = 1;
            if (eyes == "open" && state is ("idle" or "walk" or "peek" or "hang") && now < _blinkUntil) eyes = "blink";
            if (legs == "tuck" && squash != 0) squash = 0;
            if (state != "sleep")
            {
                if (now < _mouthOpenUntil) mouth = "open";
                else if (now < _chewUntil && ((int)(now * 6)) % 2 == 1) mouth = "chew";
            }
            return new FrameKey(eyes, look, arms, legs, squash, blush, mouth);
        }

        void Render(double now)
        {
            var key = Compose(now);
            if (_frameKey == null || !key.Equals(_frameKey.Value))
            {
                _frameKey = key;
                Win.SetFrame(Frame(key));
            }
            Win.MoveTo((int)Math.Round(_x) - Win.ML, (int)Math.Round(_y + _hopOff) - Win.MT);

            double rotation = 0;
            if (_state == "fly") rotation = Clamp(_vx / Dp(70), -25, 25);
            else if (_state == "hang") rotation = Math.Sin((now - _hangStart) * 3.0) * 6.0;
            else if (_expr == "dizzy" && now < _exprUntil) rotation = Math.Sin(now * 10.0) * 7.0;
            else if (_mediaKind != null && _state == "idle" && !_hopActive) rotation = Math.Sin(now * Math.PI * 2.0) * 6.0;
            else if (_state == "drag" && _touch != null && now - _touch.Hist[^1].T < 0.08)
                rotation = Clamp(_touch.Vx / Dp(90), -18, 18);
            Win.SetRotation(rotation);
        }

        void RestorePosition(double minX, double maxX, double minY, double maxY)
        {
            double fx = _cfg.PosX >= 0 ? _cfg.PosX / 1000.0 : 0.8;
            double fy = _cfg.PosY >= 0 ? _cfg.PosY / 1000.0 : 1.0;
            _x = minX + (maxX - minX) * Clamp(fx, 0, 1);
            _y = minY + (maxY - minY) * Clamp(fy, 0, 1);
        }

        void SavePosition()
        {
            if (_bounds == null) return;
            var b = _bounds.Value;
            _cfg.PosX = (int)(Clamp((_x - b.MinX) / Math.Max(1.0, b.MaxX - b.MinX), 0, 1) * 1000);
            _cfg.PosY = (int)(Clamp((_y - b.MinY) / Math.Max(1.0, b.MaxY - b.MinY), 0, 1) * 1000);
        }

        /// <summary>«Позвать сюда» — питомец появляется у курсора.</summary>
        public void Recenter()
        {
            if (!_cfg.Show)
            {
                _cfg.Show = true;
                _cfg.Save();
            }
            Start(false);
            double now = Now;
            var (cx, cy) = Native.Cursor();
            _x = cx - _w / 2.0;
            _y = cy - _h;
            _bounds = null;
            var b = Bounds(now);
            _x = Clamp(_x, b.MinX, b.MaxX);
            _y = Clamp(_y, b.MinY, b.MaxY);
            _posReady = true;
            _state = "idle";
            _vx = _vy = 0;
            _platform = null;
            _peekSide = _peekPending = 0;
            _lastInteraction = now;
            Hop(Dp(500));
            for (int i = 0; i < 3; i++) SpawnParticle("spark");
            Say("hello");
            SavePosition();
            Schedule(16);
        }
    }
}
