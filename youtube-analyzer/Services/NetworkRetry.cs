using System.Net.Http;

namespace YoutubeAnalyzer.Services;

internal static class NetworkRetry
{
    private const int MaxAttempts = 4;

    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string description,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                if (attempt >= MaxAttempts)
                {
                    Console.Error.WriteLine(
                        $"Network operation failed after {MaxAttempts} attempts while {description}.");
                    throw;
                }

                var delay = TimeSpan.FromSeconds(1 << (attempt - 1));
                Console.Error.WriteLine(
                    $"Network error while {description}; retrying in {delay.TotalSeconds:0} seconds "
                    + $"(attempt {attempt + 1} of {MaxAttempts}): {exception.Message}");

                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
