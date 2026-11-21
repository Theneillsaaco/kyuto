namespace Kyuto.Mpris;

public static class Cmd
{
    public sealed record Play;
    public sealed record Pause;
    public sealed record PlayPause;
    public sealed record Stop;
    public sealed record Next;
    public sealed record Previous;
    public sealed record SeekRelative(long Micros);
    public sealed record SeekTo(long Micros);
    public sealed record SetVolume(double Level);
    public sealed record SetLoop(string Status);
    public sealed record SetShuffle(bool On);
    public sealed record SetRate(double Rate);
}

/// <summary>A request from the desktop. Number is microseconds, a 0..1 volume or 0/1 for shuffle; Text is the loop status.</summary>
public union MprisCommand(
    Cmd.Play, Cmd.Pause, Cmd.PlayPause, Cmd.Stop, Cmd.Next, Cmd.Previous,
    Cmd.SeekRelative, Cmd.SeekTo, Cmd.SetVolume, Cmd.SetLoop, Cmd.SetShuffle, Cmd.SetRate);
