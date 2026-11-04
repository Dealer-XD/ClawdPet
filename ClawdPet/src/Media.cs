using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace ClawdPet
{
    /// <summary>Следит за системным медиаплеером Windows (Spotify, браузер, Яндекс Музыка и т.д.).</summary>
    public class MediaWatcher
    {
        volatile string _track;
        volatile bool _playing;
        CancellationTokenSource _cts;

        public bool Playing => _playing;
        public string Track => _track;

        public void Start()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            Task.Run(async () =>
            {
                GlobalSystemMediaTransportControlsSessionManager mgr = null;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        mgr ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                        var session = mgr.GetCurrentSession();
                        bool playing = false;
                        string track = null;
                        if (session != null)
                        {
                            var info = session.GetPlaybackInfo();
                            playing = info != null &&
                                      info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                            if (playing)
                            {
                                var props = await session.TryGetMediaPropertiesAsync();
                                var title = props?.Title?.Trim() ?? "";
                                var artist = props?.Artist?.Trim() ?? "";
                                track = title.Length > 0 && artist.Length > 0 ? $"{artist} — {title}"
                                    : (title.Length > 0 ? title : (artist.Length > 0 ? artist : null));
                            }
                        }
                        _playing = playing;
                        _track = track;
                    }
                    catch (Exception e)
                    {
                        _playing = false;
                        _track = null;
                        mgr = null;
                        Log.Write("media: " + e.Message);
                        try { await Task.Delay(5000, token); } catch { }
                    }
                    try { await Task.Delay(1000, token); } catch { }
                }
            }, token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
            _playing = false;
            _track = null;
        }
    }
}
