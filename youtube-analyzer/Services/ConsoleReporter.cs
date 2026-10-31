using System.Diagnostics;
using System.Text;

namespace YoutubeAnalyzer.Services;

internal enum VideoOutcome
{
    Generated,
    UpToDate,
    Skipped,
    Failed
}

/// <summary>Console output for batch semantic generation; implementations must be thread-safe.</summary>
internal interface IConsoleReporter
{
    void Begin(int total, int concurrency);
    void VideoCompleted(VideoOutcome outcome, string message, TimeSpan? duration = null);
    void End();
}

internal static class ConsoleReporterFactory
{
    /// <summary>"Progress" falls back to plain output when stdout is redirected (logs, pipes).</summary>
    public static IConsoleReporter Create(string? mode) =>
        string.Equals(mode, "Progress", StringComparison.OrdinalIgnoreCase) && !Console.IsOutputRedirected
            ? new ProgressBarConsoleReporter()
            : new PlainConsoleReporter();
}

/// <summary>One line per video, plus start and finish lines.</summary>
internal sealed class PlainConsoleReporter : IConsoleReporter
{
    private readonly Stopwatch _stopwatch = new();

    public void Begin(int total, int concurrency)
    {
        _stopwatch.Restart();
        Console.WriteLine(
            $"Semantic generation started at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} ({total} videos, concurrency {concurrency}).");
    }

    public void VideoCompleted(VideoOutcome outcome, string message, TimeSpan? duration = null)
    {
        if (outcome == VideoOutcome.Skipped)
        {
            return;
        }

        var writer = outcome == VideoOutcome.Failed ? Console.Error : Console.Out;
        writer.WriteLine(message);
    }

    public void End()
    {
        _stopwatch.Stop();
        Console.WriteLine(
            $"Semantic generation finished at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}. Duration: {_stopwatch.Elapsed:hh\\:mm\\:ss}.");
    }
}

/// <summary>
/// Single live progress line; generated and failed videos are printed above it,
/// up-to-date ones are only counted.
/// </summary>
internal sealed class ProgressBarConsoleReporter : IConsoleReporter
{
    private const int BarWidth = 30;

    private readonly object _gate = new();
    private readonly Stopwatch _stopwatch = new();
    private int _total;
    private int _completed;
    private int _generated;
    private int _upToDate;
    private int _failed;
    private int _lastLineLength;
    private TimeSpan _generatedTime;

    public void Begin(int total, int concurrency)
    {
        lock (_gate)
        {
            _total = total;
            _stopwatch.Restart();
            Console.WriteLine(
                $"Semantic generation started at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} ({total} videos, concurrency {concurrency}).");
            Render();
        }
    }

    public void VideoCompleted(VideoOutcome outcome, string message, TimeSpan? duration = null)
    {
        lock (_gate)
        {
            _completed++;

            switch (outcome)
            {
                case VideoOutcome.Generated:
                    _generated++;
                    _generatedTime += duration ?? TimeSpan.Zero;
                    break;
                case VideoOutcome.UpToDate:
                    _upToDate++;
                    break;
                case VideoOutcome.Failed:
                    _failed++;
                    break;
            }

            if (outcome is VideoOutcome.Generated or VideoOutcome.Failed)
            {
                ClearLine();
                (outcome == VideoOutcome.Failed ? Console.Error : Console.Out).WriteLine(message);
            }

            Render();
        }
    }

    public void End()
    {
        lock (_gate)
        {
            _stopwatch.Stop();
            ClearLine();
            Console.WriteLine(
                $"Semantic generation finished at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}. Duration: {_stopwatch.Elapsed:hh\\:mm\\:ss}. "
                + $"Generated: {_generated}, up to date: {_upToDate}, failed: {_failed}.");
        }
    }

    private void Render()
    {
        var fraction = _total == 0 ? 1d : (double)_completed / _total;
        var filled = (int)Math.Round(fraction * BarWidth);
        var elapsed = _stopwatch.Elapsed;
        var eta = _completed > 0 && _completed < _total
            ? TimeSpan.FromTicks(elapsed.Ticks / _completed * (_total - _completed))
            : TimeSpan.Zero;

        var average = _generated > 0
            ? TimeSpan.FromTicks(_generatedTime.Ticks / _generated)
            : TimeSpan.Zero;

        var line = new StringBuilder()
            .Append('[').Append('#', filled).Append('-', BarWidth - filled).Append("] ")
            .Append($"{_completed}/{_total} ({fraction:P0}) ")
            .Append($"gen {_generated} ok {_upToDate} fail {_failed} ")
            .Append($"| AVG {average.TotalSeconds:0.0}s ")
            .Append($"| {FormatDuration(elapsed)} | ETA {FormatDuration(eta)}")
            .ToString();

        ClearLine();
        Console.Write(line);
        _lastLineLength = line.Length;
    }

    private static string FormatDuration(TimeSpan value) =>
        $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";

    private void ClearLine()
    {
        if (_lastLineLength > 0)
        {
            Console.Write("\r" + new string(' ', _lastLineLength) + "\r");
            _lastLineLength = 0;
        }
    }
}
