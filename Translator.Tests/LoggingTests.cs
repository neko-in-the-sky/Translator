// UseWPF projects drop System.IO from the implicit usings, because System.Windows.Shapes.Path
// would collide with System.IO.Path, so it has to be imported explicitly here.
using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog;
using Translator.Logging;

namespace Translator.Tests;

/// <summary>
/// These tests point LOCALAPPDATA at a temp directory, which changes it for the whole test process,
/// so they must not run alongside other tests.
/// </summary>
[CollectionDefinition(nameof(LocalAppDataCollection), DisableParallelization = true)]
public class LocalAppDataCollection
{
}

/// <summary>
/// Checks the logging section of the shipped appsettings.json, not a copy of it, so that a change
/// to the real file is what these tests catch. xUnit creates one instance per test, so each test
/// gets its own LOCALAPPDATA.
/// </summary>
[Collection(nameof(LocalAppDataCollection))]
public class LoggingTests : IDisposable
{
    private readonly string _localAppData;
    private readonly string? _originalLocalAppData;

    public LoggingTests()
    {
        _localAppData = Path.Combine(Path.GetTempPath(), "translator-logging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_localAppData);
        _originalLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _localAppData);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _originalLocalAppData);
        Directory.Delete(_localAppData, recursive: true);
    }

    private string LogsDirectory => Path.Combine(_localAppData, "Translator", "Logs");

    private static IConfigurationRoot ShippedConfiguration(IDictionary<string, string?>? overrides = null)
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false);
        if (overrides != null)
        {
            builder.AddInMemoryCollection(overrides);
        }

        return builder.Build();
    }

    private static IConfigurationSection FileSink(IConfiguration configuration) =>
        configuration.GetSection("Serilog:WriteTo").GetChildren()
            .Single(sink => string.Equals(sink["Name"], "File", StringComparison.OrdinalIgnoreCase));

    private string[] LogFiles() => Directory.Exists(LogsDirectory)
        ? Directory.GetFiles(LogsDirectory, "log-*.txt")
        : [];

    [Fact]
    public void ShippedConfig_WritesLogsUnderLocalAppData()
    {
        using (var logger = new LoggerConfiguration().ReadFrom.Configuration(ShippedConfiguration()).CreateLogger())
        {
            logger.Information("Written by {Test}", nameof(ShippedConfig_WritesLogsUnderLocalAppData));
        }

        var file = Assert.Single(LogFiles());
        Assert.Contains(nameof(ShippedConfig_WritesLogsUnderLocalAppData), File.ReadAllText(file));
    }

    [Fact]
    public void ShippedConfig_RollsOnSizeAndKeepsTenFiles()
    {
        // Only the size limit changes, so rolling happens after a few events instead of 10 MB.
        // Each event is about 100 bytes: Serilog drops an event that is bigger than the limit.
        var configuration = ShippedConfiguration();
        var sizeLimitKey = FileSink(configuration).Path + ":Args:fileSizeLimitBytes";
        configuration = ShippedConfiguration(new Dictionary<string, string?> { [sizeLimitKey] = "1024" });

        using (var logger = new LoggerConfiguration().ReadFrom.Configuration(configuration).CreateLogger())
        {
            // About 30 KB, so about 30 files are started and all but the newest 10 are deleted.
            for (var i = 0; i < 300; i++)
            {
                logger.Information("Event {Number} padded to roughly a hundred bytes", i);
            }
        }

        Assert.Equal(10, LogFiles().Length);
    }

    [Fact]
    public void ShippedConfig_LimitsFileSizeTo10MB()
    {
        var args = FileSink(ShippedConfiguration()).GetSection("Args");

        Assert.Equal("10485760", args["fileSizeLimitBytes"]);
        Assert.Equal("Day", args["rollingInterval"]);
    }

    private static IConfiguration OneSink(string name, string? path) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:WriteTo:0:Name"] = name,
            ["Serilog:WriteTo:0:Args:path"] = path
        })
        .Build();

    [Fact]
    public void LogFolder_FindsShippedFolder()
    {
        Assert.Equal(LogsDirectory, LogFolder.Find(ShippedConfiguration()));
    }

    [Fact]
    public void LogFolder_ResolvesRelativePathAgainstBaseDirectory()
    {
        // The app makes AppContext.BaseDirectory current, which is where Serilog puts a relative path.
        Assert.Equal(
            Path.Combine(AppContext.BaseDirectory, "logs"),
            LogFolder.Find(OneSink("File", @"logs\log-.txt")));
    }

    [Theory]
    [InlineData("Console", "log.txt")]
    [InlineData("File", null)]
    [InlineData("File", " ")]
    public void LogFolder_ReturnsNullWithoutFileSink(string name, string? path)
    {
        Assert.Null(LogFolder.Find(OneSink(name, path)));
    }
}
