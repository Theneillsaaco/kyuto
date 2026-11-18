using Kyuto.Core;
using Kyuto.Core.Playback;
using Kyuto.Mpris;

namespace Kyuto.App.Services;

// The desktop's side of the player (playerctl, media keys, widgets). What is told to the desktop is built by
// MprisMapper. PlaybackService is partial so this lives next to the feature and shares its state.
internal sealed partial class PlaybackService
{
    public void StartMpris()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var service = new MprisService();
        _ = Task.Run(async () =>
        {
            try
            {
                await service.StartAsync();
                _mpris = service;
            }
            catch (Exception ex)
            {
                Log.Warn("playback", "mpris unavailable", ex);
                service.Dispose();
            }
        });
    }

    /// <summary>
    /// Runs what the desktop asked for (playerctl, media keys). QML calls it from an always-on timer,
    /// because a command can arrive while the player is idle and the main poll is not running.
    /// </summary>
    public void Pump()
    {
        PumpWaiting();
        PumpCore();
    }

    private void PumpCore()
    {
        if (_mpris is not { } mpris)
            return;

        var any = false;
        while (mpris.TryDequeue(out var command))
        {
            any = true;
            try
            {
                Handle(command);
            }
            catch (Exception ex)
            {
                Log.Warn("playback", "mpris command", ex);
            }
        }
        if (any)
            Poll();
        else if (!_mprisPublished)
            PublishMpris();
    }

    private void Handle(MprisCommand command)
    {
        switch (command.Action)
        {
            case MprisAction.Play:
                if (!IsLoaded || Paused)
                    Toggle();
                break;
            case MprisAction.Pause:
                if (IsLoaded && !Paused)
                    Toggle();
                break;
            case MprisAction.PlayPause: Toggle(); break;
            case MprisAction.Stop:
                if (IsLoaded && _awaitingId is null)
                {
                    _engine.SetPaused(true);
                    _engine.Seek(0);
                    _mpris?.Seeked(0);
                }
                break;
            case MprisAction.Next: Next(); break;
            case MprisAction.Previous: Previous(); break;
            case MprisAction.SeekRelative: Seek(Math.Clamp(Position + command.Number / 1e6, 0, Duration)); break;
            case MprisAction.SeekTo: Seek(Math.Clamp(command.Number / 1e6, 0, Duration)); break;
            case MprisAction.SetVolume: SetVolume((int)Math.Round(Math.Clamp(command.Number, 0, 1) * 100)); break;
            case MprisAction.SetRate: ChooseSpeed(command.Number); break;
            case MprisAction.SetShuffle:
                if (_queue.Shuffle != command.Number > 0) ToggleShuffle();
                break;
            case MprisAction.SetLoop:
                _queue = PlayQueue.setRepeat(MprisMapper.RepeatOf(command.Text), _queue);
                PersistQueue();
                AfterQueueEdit();
                break;
        }
        Poll();
    }

    private void PublishMpris()
    {
        if (_mpris is not { } mpris)
            return;

        _mprisPublished = true;

        mpris.Publish(MprisMapper.Build(
            CurrentTrack, IsLoaded, Paused, awaitingStream: _awaitingId is not null, Duration, Position, Volume,
            Speed, RepeatMode, Shuffle, CanNext, CanPrevious, QueueCount));
    }
}
