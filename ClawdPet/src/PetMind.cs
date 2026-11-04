using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClawdPet
{
    public partial class Pet
    {
        // ---------- мозг ----------

        static readonly HashSet<string> GenericUsers = new(StringComparer.OrdinalIgnoreCase)
        {
            "user", "admin", "administrator", "owner", "пользователь", "администратор", "home", "pc", "guest",
        };

        void BrainInit()
        {
            try
            {
                _brain.Load();
                if (_brain.Data.Created <= 0)
                {
                    int seeded = 0;
                    foreach (var (kind, count) in new[] { ("pet", _cfg.PetsCount), ("eat", _cfg.EatenCount), ("clean", _cfg.PoopsCleaned) })
                        if (count > 0) { _brain.Data.Stats[kind] = count; seeded += count; }
                    if (seeded > 0)
                        _brain.Data.Bond = Math.Min(45.0, C.BondStart + _brain.Data.Stats.GetValueOrDefault("pet") * 0.4
                                                         + _brain.Data.Stats.GetValueOrDefault("clean") * 0.5);
                }
                _aiHistory = _brain.RecentSaid(C.AiHistory);
                if (_cfg.BrainEnabled)
                {
                    var win = Environment.UserName;
                    if (!string.IsNullOrWhiteSpace(win) && !GenericUsers.Contains(win) && win.Length <= 24)
                        _brain.Data.TgName = win;
                    _brain.SessionStart();
                    BrainDirty();
                }
            }
            catch (Exception e) { Log.Write("brain init: " + e.Message); }
        }

        public Brain BrainRef => _brain;

        string UserName()
        {
            if (!string.IsNullOrWhiteSpace(_cfg.BrainName)) return _cfg.BrainName.Trim();
            var n = _brain.Data.TgName;
            return string.IsNullOrWhiteSpace(n) ? null : n;
        }

        public string DescribeMemory() => _brain.Describe(UserName());

        /// <summary>Портрет для меню в стиле Claude Code.</summary>
        public BitmapSource Portrait(int cell = 6)
        {
            var pal = Palette;
            return Sprites.RenderFrame(new FrameKey("happy", 0, "normal", "stand", 0, true, null), cell, pal.Body, pal.Eye).ToBitmap();
        }

        public string DisplayName() => UserName();

        public string BondLabel() => _brain.BondLabel();

        public List<(string Time, string Text)> RecentActivity(int limit = 4) => _brain.RecentPairs(limit);

        void BrainDirty()
        {
            if (!_brainTimer.IsEnabled) _brainTimer.Start();
        }

        public void BrainFlush()
        {
            try { if (_brain.Dirty) _brain.Save(); }
            catch (Exception e) { Log.Write("brain flush: " + e.Message); }
        }

        public void BrainForget()
        {
            _brain.Forget();
            _aiHistory = new();
            BrainFlush();
            if (_running) ShowBubble("Кто я? Где я? Привет!");
        }

        public bool BrainImport(string raw)
        {
            if (!_brain.Load(raw)) return false;
            _aiHistory = _brain.RecentSaid(C.AiHistory);
            BrainFlush();
            if (_running) ShowBubble("О! Я всё вспомнил ♥");
            return true;
        }

        void Mem(string kind, string detail = null, double bond = 0.0)
        {
            if (!_cfg.BrainEnabled) return;
            try
            {
                _brain.Record(kind, detail, bond);
                BrainDirty();
            }
            catch (Exception e) { Log.Write("brain record: " + e.Message); }
        }

        void RememberTrack(string track)
        {
            if (!_cfg.BrainEnabled) return;
            _brain.Track(track);
            Mem("music", track);
        }

        string BrainPrompt()
        {
            if (!_cfg.BrainEnabled || !_cfg.BrainShare) return "";
            try { return _brain.PromptBlock(UserName(), _cfg.BrainAbout); }
            catch (Exception e) { Log.Write("brain prompt: " + e.Message); return ""; }
        }

        string BrainStatic(string category, string name)
        {
            if (!_cfg.BrainEnabled) return null;
            string user = UserName();
            double roll = _rng.NextDouble();
            if (category == "hello" && user != null && roll < 0.7)
            {
                var options = new List<string> { $"Привет, {user}!", $"{user}, это ты!" };
                int days = _brain.DaysTogether();
                if (days >= 3) options.Add($"Мы вместе уже {days} {Brain.Plural(days, "день", "дня", "дней")}");
                return Pick(options);
            }
            if (category == "back" && user != null && roll < 0.7)
                return Pick(new[] { $"{user}, я скучал!", $"С возвращением, {user}!" });
            if (category == "pet")
            {
                int count = _brain.Data.Stats.GetValueOrDefault("pet");
                if (count >= 25 && roll < 0.25) return $"Ты гладил меня {count} {Brain.Plural(count, "раз", "раза", "раз")}. Ещё!";
            }
            if (category == "app_again" && name != null)
            {
                int visits = _brain.Visits(_appKey);
                if (visits >= 3 && roll < 0.5) return $"{name}: ты тут уже {visits} {Brain.Plural(visits, "раз", "раза", "раз")}";
            }
            return null;
        }

        // ---------- реплики и ИИ ----------

        bool AiReady() => _cfg.AiEnabled && (!string.IsNullOrWhiteSpace(_cfg.AiKey) || !string.IsNullOrWhiteSpace(_cfg.Ai2Url));

        List<int> AiProviders(double now)
        {
            var result = new List<int>();
            if (!string.IsNullOrWhiteSpace(_cfg.AiKey) && now >= _aiBo[0]) result.Add(0);
            if (!string.IsNullOrWhiteSpace(_cfg.Ai2Url) && now >= _aiBo[1]) result.Add(1);
            return result;
        }

        AiProviderCfg ProviderCfg(int index) => index == 0
            ? new AiProviderCfg { Key = _cfg.AiKey, Url = _cfg.AiUrl, Model = _cfg.AiModel, Provider = _cfg.AiProvider, Persona = _cfg.AiPersona }
            : new AiProviderCfg { Key = _cfg.Ai2Key ?? "", Url = _cfg.Ai2Url, Model = _cfg.Ai2Model, Provider = 1, Persona = _cfg.AiPersona };

        string PositionLabel()
        {
            if (_bounds == null) return null;
            var b = _bounds.Value;
            double fx = (_x - b.MinX) / Math.Max(1.0, b.MaxX - b.MinX);
            double fy = (_y - b.MinY) / Math.Max(1.0, b.MaxY - b.MinY);
            string h = fx < 0.2 ? "у левого края" : fx > 0.8 ? "у правого края" : "в середине по горизонтали";
            string v = _platform != null ? "на крыше окна" : fy < 0.25 ? "вверху" : fy > 0.75 ? "внизу, на панели задач" : "посередине по высоте";
            return $"{h}, {v}";
        }

        void Say(string category, bool force = false, string name = null, Dictionary<string, string> ctx = null)
        {
            if (!_running || (!force && !_cfg.Phrases)) return;
            if (!AiReady())
            {
                SayStatic(category, name);
                return;
            }
            double now = Now;
            bool idle = category == "idle";
            double gap = 0;
            if (idle)
            {
                gap = C.AiChatGaps[(int)Clamp(_cfg.AiChat, 0, C.AiChatGaps.Length - 1)];
                if (gap <= 0 || now < _aiIdleNext) return;
            }
            var providers = AiProviders(now);
            if (providers.Count == 0)
            {
                if (_cfg.AiFallback) SayStatic(category, name);
                return;
            }
            if (_aiBusy || (!force && now < _aiNext)) return;
            ctx = ctx != null ? new Dictionary<string, string>(ctx) : new Dictionary<string, string>();
            if (name != null && !ctx.ContainsKey("name")) ctx["name"] = name;
            if (_cfg.AiMove && !ctx.ContainsKey("position")) ctx["position"] = PositionLabel();
            _aiBusy = true;
            _aiNext = now + C.AiCooldown;
            if (idle) _aiIdleNext = now + gap;
            int gen = ++_sayGen;
            var chain = providers.Select(i => (i, ProviderCfg(i))).ToList();
            var history = _aiHistory.ToList();
            var memory = BrainPrompt();
            var dispatcher = Win.Dispatcher;
            Task.Run(async () =>
            {
                AiResult res = null;
                string err = null;
                try
                {
                    foreach (var (index, pcfg) in chain)
                    {
                        try
                        {
                            res = await Ai.Call(pcfg, category, ctx, history, memory);
                            if (res.Say != null || res.Action != null) break;
                        }
                        catch (AiHttpError e)
                        {
                            Log.Write($"ai[{index}] http {e.Code}: {(e.Body.Length > 300 ? e.Body[..300] : e.Body)}");
                            double t = Now;
                            if (e.Code == 429) { _aiBo[index] = t + C.AiBackoff; err = "ИИ: лимит запросов, подожду"; }
                            else if (e.Code is 400 or 401 or 403 or 404) { _aiBo[index] = t + C.AiBackoff * 5; err = C.AiErrText; }
                            else if (e.Code >= 500) _aiBo[index] = t + 30.0;
                        }
                        catch (Exception e)
                        {
                            Log.Write($"ai[{index}] error: {e.Message}");
                            _aiBo[index] = Now + 20.0;
                        }
                    }
                }
                finally { _aiBusy = false; }
                bool ok = res != null && (res.Say != null || res.Action != null);
                if (ok) err = null;
                await dispatcher.InvokeAsync(() =>
                {
                    if (ok && res.Remember != null) BrainLearn(res.Remember);
                    AiDeliver(ok ? res : null, err, category, name, gen);
                });
            });
        }

        void BrainLearn(string remember)
        {
            if (!_cfg.BrainEnabled || !_cfg.BrainLearn) return;
            if (_brain.AddNote(remember)) Log.Write("brain learned: " + remember);
            BrainDirty();
        }

        void AiDeliver(AiResult res, string err, string category, string name, int gen)
        {
            if (!_running || gen != _sayGen) return;
            if (res != null)
            {
                if (res.Say != null)
                {
                    _aiHistory.Add(res.Say);
                    if (_aiHistory.Count > C.AiHistory) _aiHistory.RemoveRange(0, _aiHistory.Count - C.AiHistory);
                    if (_cfg.BrainEnabled) { _brain.AddSaid(res.Say); BrainDirty(); }
                    _lastPhrase = res.Say;
                    ShowBubble(res.Say);
                }
                if (_cfg.AiMoods) ApplyMood(res.Mood);
                if (_cfg.AiMove) ApplyAction(res.Action);
                return;
            }
            double now = Now;
            if (err != null && now >= _aiErrNext)
            {
                _aiErrNext = now + C.AiErrGap;
                ShowBubble(err);
                return;
            }
            if (_cfg.AiFallback) SayStatic(category, name);
        }

        public void AiTest()
        {
            _aiBo[0] = _aiBo[1] = 0;
            _aiNext = _aiIdleNext = _aiErrNext = 0;
            if (!_running) Start(false);
            if (!AiReady())
            {
                ShowBubble("Включи ИИ и вставь API-ключ");
                return;
            }
            Say("hello", force: true);
        }

        void SayStatic(string category, string name)
        {
            var memo = BrainStatic(category, name);
            if (memo != null && memo != _lastPhrase)
            {
                _lastPhrase = memo;
                ShowBubble(memo);
                return;
            }
            var phrases = C.Phrases.TryGetValue(category, out var list) ? list.ToList() : new List<string> { category };
            if (name == null)
            {
                var noName = phrases.Where(p => !p.Contains("{name}")).ToList();
                if (noName.Count > 0) phrases = noName;
            }
            var options = phrases.Where(p => p != _lastPhrase).ToList();
            if (options.Count == 0) options = phrases;
            var text = Pick(options).Replace("{name}", name ?? "");
            _lastPhrase = text;
            ShowBubble(text);
        }

        void ApplyAction(string action)
        {
            if (action == null || action == "none" || !C.Actions.Contains(action)) return;
            if (_state is not ("idle" or "walk") || _hopActive || _touch != null || _bounds == null) return;
            var b = _bounds.Value;
            double now = Now;
            switch (action)
            {
                case "walk_left":
                case "walk_right":
                case "go_left_edge":
                case "go_right_edge":
                case "center":
                {
                    double target = action switch
                    {
                        "walk_left" => _x - U(Dp(80), Dp(260)),
                        "walk_right" => _x + U(Dp(80), Dp(260)),
                        "go_left_edge" => b.MinX,
                        "go_right_edge" => b.MaxX,
                        _ => (b.MinX + b.MaxX) / 2.0,
                    };
                    var (lo, hi) = WalkRange(b.MinX, b.MaxX);
                    target = Clamp(target, lo, hi);
                    if (Math.Abs(target - _x) < Dp(12)) return;
                    _walkTarget = target;
                    _peekPending = 0;
                    _state = "walk";
                    _look = target > _x ? 1 : -1;
                    break;
                }
                case "peek_left":
                case "peek_right":
                {
                    if (_platform != null) return;
                    int side = action == "peek_left" ? -1 : 1;
                    _walkTarget = side < 0 ? b.MinX : b.MaxX;
                    _peekPending = side;
                    _state = "walk";
                    break;
                }
                case "hop": Hop(Dp(500)); break;
                case "sleep":
                    if (_state != "idle") return;
                    _state = "sleep";
                    _look = 0;
                    _nextZ = now + 0.6;
                    break;
                case "look_left": _look = -1; break;
                case "look_right": _look = 1; break;
            }
            _lastInteraction = now;
            _nextAction = Math.Max(_nextAction, now + U(3.0, 6.0));
            Schedule(0);
        }

        void ApplyMood(string mood)
        {
            if (mood == null || mood == "neutral" || !C.Moods.Contains(mood)) return;
            double now = Now;
            switch (mood)
            {
                case "happy": SetExpr("happy", 2.0); SpawnParticle("spark"); break;
                case "love": SetExpr("happy", 2.4); for (int i = 0; i < 3; i++) SpawnParticle("heart"); break;
                case "laugh": SetExpr("happy", 1.8); Hop(Dp(420)); SpawnParticle("spark"); break;
                case "excited": SetExpr("happy", 1.8); Hop(Dp(720)); for (int i = 0; i < 3; i++) SpawnParticle("spark"); break;
                case "surprised": SetExpr("surprised", 1.2); Hop(Dp(300)); break;
                case "scared": SetExpr("surprised", 1.8); _squashUntil = now + 0.15; _look = Pick(new[] { -1, 1 }); break;
                case "angry": SetExpr("angry", 2.2); break;
                case "sad": SetExpr("sad", 2.8); break;
                case "sleepy": SetExpr("sleep", 2.2); SpawnParticle("zzz"); break;
                case "wave": _waveUntil = now + 1.8; break;
                case "dizzy": SetExpr("dizzy", 2.4); break;
                case "music": SetExpr("happy", 2.0); for (int i = 0; i < 3; i++) SpawnParticle("note"); Hop(Dp(300)); break;
            }
            Schedule(0);
        }
    }
}
