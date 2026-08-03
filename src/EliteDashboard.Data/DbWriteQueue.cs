using System.Threading.Channels;

namespace EliteDashboard.Data;

/// <summary>
/// SQLite only allows one writer at a time, and DbContext isn't thread-safe.
/// Multiple BackgroundServices (journal tailer, later EDDN listener, etc.) all
/// want to write - rather than let them race, they enqueue work here and one
/// dedicated DbWriterService drains it serially. Reads can still use
/// IDbContextFactory directly and concurrently; only writes are serialized.
/// </summary>
public sealed class DbWriteQueue
{
    private readonly Channel<(Func<DashboardDbContext, CancellationToken, Task> Work, TaskCompletionSource Completion)> _channel =
        Channel.CreateUnbounded<(Func<DashboardDbContext, CancellationToken, Task>, TaskCompletionSource)>();

    public Task EnqueueAsync(Func<DashboardDbContext, CancellationToken, Task> work)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _channel.Writer.TryWrite((work, completion));
        return completion.Task;
    }

    internal ChannelReader<(Func<DashboardDbContext, CancellationToken, Task> Work, TaskCompletionSource Completion)> Reader => _channel.Reader;
}
