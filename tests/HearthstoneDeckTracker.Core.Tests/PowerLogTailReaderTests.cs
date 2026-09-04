using HearthstoneDeckTracker.Core.Logging;

namespace HearthstoneDeckTracker.Core.Tests;

public class PowerLogTailReaderTests
{
    [Fact]
    public async Task ReadLinesAsync_ReadsExistingAndAppendedLines()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hdt-tail-{Guid.NewGuid():N}.log");
        await File.WriteAllTextAsync(path, "first" + Environment.NewLine);

        try
        {
            var reader = new PowerLogTailReader(path, startAtEnd: false, pollInterval: TimeSpan.FromMilliseconds(40));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var lines = new List<string>();

            await foreach (var line in reader.ReadLinesAsync(cts.Token))
            {
                lines.Add(line);
                if (lines.Count == 1)
                    await File.AppendAllTextAsync(path, "second" + Environment.NewLine);
                if (lines.Count >= 2)
                    break;
            }

            Assert.Equal(["first", "second"], lines);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
