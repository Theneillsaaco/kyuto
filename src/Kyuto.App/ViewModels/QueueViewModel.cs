using Kyuto.App.Models;
using Kyuto.App.Services;
using Kyuto.App.ViewModels.Base;
using Kyuto.Core.Playback;

namespace Kyuto.App.ViewModels;

/// <summary>The queue panel: rows in play order, how many there are and which one is playing.</summary>
public class QueueViewModel : ViewModelBase
{
    private int _revision = -1;
    private QueueState? _undo;
    
    public QueueViewModel()
    {
        AppServices.EnsureStarted();
        AppServices.Playback.Changed += Refresh;
        Refresh();
    }

    public TrackListModel Items { get; } = new();
    public int Count { get; private set => Set(ref field, value); }
    /// <summary>Position of the current track in the panel (play order), -1 if none.</summary>
    public int Position { get; private set => Set(ref field, value); } = -1;

    public string TotalText { get; private set => Set(ref field, value); } = "";
    
    public void PlayItem(int position) => AppServices.Playback.PlayQueueItem(position);

    /// <summary>Drag and drop: the entry at <paramref name="source"/> ends up at <paramref name="target"/>.</summary>
    public void MoveItem(int source, int target) => AppServices.Playback.MoveQueueItem(source, target);

    public void RemoveItem(int position) 
    {
        _undo = AppServices.Playback.QueueSnapshot;
        AppServices.Playback.RemoveQueueItem(position);
    }
    
    public void ClearAll()   
    {
        _undo = AppServices.Playback.QueueSnapshot;
        AppServices.Playback.ClearQueue();
    }

    public void UndoLast()
    {
        if (_undo is not { } snapshot)
            return;

        _undo = null;
        AppServices.Playback.RestoreQueue(snapshot);
    }
    
    private static string Span(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        
        return t.TotalHours >= 1 
            ? Tr.F("queue.hoursMinutes", (int)t.TotalHours, t.Minutes)
            : Tr.F("queue.minutes", Math.Max(1, (int)Math.Round(t.TotalMinutes)));
    }
    
    private void Refresh()
    {
        var p = AppServices.Playback;
        if (p.QueueRevision != _revision)
        {
            _revision = p.QueueRevision;
            Items.Replace(p.Ordered.Select(Rows.FromTrack));
            TotalText = p.QueueCount == 0 ? "" : Span(p.Ordered.Sum(t => t.DurationSeconds));
        }
        Count = p.QueueCount;
        Position = p.QueuePosition;
    }
}
