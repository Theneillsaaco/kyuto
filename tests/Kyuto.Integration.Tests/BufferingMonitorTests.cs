using Player.App.Services;

namespace Player.Integration.Tests;

public class BufferingMonitorTests
{
    [Fact]
    public void Not_waiting_shows_nothing()
    {
        var monitor = new BufferingMonitor();

        monitor.Update(false, 10);

        Assert.False(monitor.Waiting);
        Assert.False(monitor.Showing);
    }

    [Fact]
    public void Waiting_is_immediate_but_the_bar_waits_a_moment()
    {
        var monitor = new BufferingMonitor(0.4);

        monitor.Update(true, 10.0);
        Assert.True(monitor.Waiting);
        Assert.False(monitor.Showing);

        monitor.Update(true, 10.3);
        Assert.False(monitor.Showing);

        monitor.Update(true, 10.5);
        Assert.True(monitor.Showing);
    }

    [Fact]
    public void Stopping_resets_the_clock()
    {
        var monitor = new BufferingMonitor(0.4);
        monitor.Update(true, 10.0);
        monitor.Update(true, 11.0);
        Assert.True(monitor.Showing);

        monitor.Update(false, 11.1);
        Assert.False(monitor.Showing);

        monitor.Update(true, 12.0); // a new wait starts from zero
        Assert.True(monitor.Waiting);
        Assert.False(monitor.Showing);
    }

    [Fact]
    public void A_wait_that_starts_at_time_zero_still_counts()
    {
        var monitor = new BufferingMonitor(0.4);

        monitor.Update(true, 0.0);
        monitor.Update(true, 0.5);

        Assert.True(monitor.Showing);
    }

}