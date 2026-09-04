using HearthstoneDeckTracker.Core;
using HearthstoneDeckTracker.Core.Logging;
using Xunit;

namespace HearthstoneDeckTracker.Core.Tests;

public class LogConfigWriterTests
{
    [Fact]
    public void DefaultPath_UsesLocalAppDataBlizzardHearthstone()
    {
        var path = HearthstonePaths.DefaultLogConfigPath;
        Assert.EndsWith(Path.Combine("Blizzard", "Hearthstone", "log.config"), path);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("<Hearthstone install>/Logs/Power.log", false)]
    [InlineData(@"C:\Program Files (x86)\Hearthstone\Logs\Power.log", true)]
    public void IsUsablePowerLogPath_RejectsPlaceholder(string? path, bool expected)
    {
        Assert.Equal(expected, HearthstonePaths.IsUsablePowerLogPath(path));
    }

    [Fact]
    public void Ensure_WritesPowerAndLoadingScreenSections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hdt-logconfig-{Guid.NewGuid():N}", "log.config");
        try
        {
            new LogConfigWriter(path).Ensure();
            var text = File.ReadAllText(path);

            Assert.Contains("[Power]", text);
            Assert.Contains("[LoadingScreen]", text);
            Assert.Contains("LogLevel=1", text);
            Assert.Contains("FilePrinting=True", text);
            Assert.Contains("Verbose=True", text);
        }
        finally
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Merge_PreservesUnrelatedSectionsAndOverridesPowerKeys()
    {
        const string existing =
            "[Zone]\r\nLogLevel=1\r\nFilePrinting=False\r\n\r\n[Power]\r\nLogLevel=0\r\nFilePrinting=False\r\n";

        var merged = LogConfigWriter.MergeRequiredSections(existing);

        Assert.Contains("[Zone]", merged);
        Assert.Contains("FilePrinting=False", merged);
        Assert.Contains("[Power]", merged);
        Assert.Contains("LogLevel=1", merged);
        Assert.Contains("FilePrinting=True", merged);
        Assert.Contains("Verbose=True", merged);
        Assert.Contains("[LoadingScreen]", merged);
    }
}
