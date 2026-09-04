using System.Runtime.CompilerServices;
using System.Text;

namespace HearthstoneDeckTracker.Core.Logging;

/// <summary>
/// Tails a log file with <see cref="FileShare.ReadWrite"/> so Hearthstone can keep writing <c>Power.log</c>.
/// </summary>
/// <remarks>
/// Skeleton only: no game-boundary detection, no persisted offset, no install-path discovery.
/// </remarks>
public sealed class PowerLogTailReader
{
    private readonly string _path;
    private readonly bool _startAtEnd;
    private readonly TimeSpan _pollInterval;

    public PowerLogTailReader(string path, bool startAtEnd = true, TimeSpan? pollInterval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _startAtEnd = startAtEnd;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(200);
    }

    public string Path => _path;

    /// <summary>
    /// Yields complete lines as they appear. Waits if the file is missing; reopens after I/O errors.
    /// </summary>
    public async IAsyncEnumerable<string> ReadLinesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!File.Exists(_path))
            {
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await foreach (var line in ReadOpenFileAsync(cancellationToken).ConfigureAwait(false))
                yield return line;
        }
    }

    private async IAsyncEnumerable<string> ReadOpenFileAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.SequentialScan);
        }
        catch (IOException)
        {
            // TODO: Distinguish sharing violations vs. the file being replaced between matches.
            await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            yield break;
        }

        await using (stream)
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
        {
            if (_startAtEnd)
                stream.Seek(0, SeekOrigin.End);

            long lastLength = stream.Length;

            while (!cancellationToken.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    yield break;
                }

                if (line is not null)
                {
                    lastLength = stream.Length;
                    yield return line;
                    continue;
                }

                long length;
                try
                {
                    length = stream.Length;
                }
                catch (IOException)
                {
                    yield break;
                }

                if (length < lastLength)
                {
                    // New Hearthstone session often truncates or replaces Power.log.
                    stream.Seek(0, SeekOrigin.Begin);
                    reader.DiscardBufferedData();
                    lastLength = length;
                    continue;
                }

                lastLength = length;
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
