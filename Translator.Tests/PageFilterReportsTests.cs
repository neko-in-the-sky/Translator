using Microsoft.Extensions.Logging;

namespace Translator.Tests;

public class PageFilterReportsTests
{
    private const string Url = "https://www.oxfordlearnersdictionaries.com/definition/english/house_1";

    private readonly CapturingLogger _logger = new();
    private readonly PageFilterReports _reports;

    public PageFilterReportsTests()
    {
        _reports = new PageFilterReports(_logger);
    }

    private static string Message(string selector = "#ox-header", string reason = "no-match") =>
        $$"""{"type":"page-filter","engine":"Oxford","selector":"{{selector}}","reason":"{{reason}}"}""";

    [Theory]
    [InlineData("no-match")]
    [InlineData("invalid-selector")]
    public void Handle_Report_LogsOneWarning(string reason)
    {
        Assert.True(_reports.Handle(Message(reason: reason), Url));

        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("#ox-header", entry.Message);
        Assert.Contains("Oxford", entry.Message);
    }

    [Fact]
    public void Handle_SameReportTwice_LogsOnce()
    {
        _reports.Handle(Message(), Url);

        Assert.False(_reports.Handle(Message(), Url));
        Assert.Single(_logger.Entries);
    }

    [Fact]
    public void Handle_DifferentSelectors_LogsEach()
    {
        _reports.Handle(Message("#ox-header"), Url);
        _reports.Handle(Message("#searchbar"), Url);

        Assert.Equal(2, _logger.Entries.Count);
    }

    [Theory]
    [InlineData("\"hello\"")]                                                                  // a plain string
    [InlineData("not json")]
    [InlineData("""{"type":"other","engine":"Oxford","selector":"#a","reason":"no-match"}""")]
    [InlineData("""{"type":"page-filter","engine":"Oxford","selector":"#a","reason":"other"}""")]
    [InlineData("""{"type":"page-filter","engine":"Oxford","reason":"no-match"}""")]          // no selector
    [InlineData("""{"type":"page-filter","engine":1,"selector":"#a","reason":"no-match"}""")] // not a string
    public void Handle_OtherMessage_IsIgnored(string message)
    {
        Assert.False(_reports.Handle(message, Url));
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void Handle_LongSelector_IsTruncated()
    {
        var selector = new string('a', 5000);

        _reports.Handle(Message(selector), Url);

        var entry = Assert.Single(_logger.Entries);
        Assert.DoesNotContain(selector, entry.Message);
        Assert.True(entry.Message.Length < 1000);
    }

    private class CapturingLogger : ILogger<PageFilterReports>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
