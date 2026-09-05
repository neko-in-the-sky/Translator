// UseWPF projects drop System.IO from the implicit usings, because System.Windows.Shapes.Path
// would collide with System.IO.Path, so it has to be imported explicitly here.
using System.IO;
using Translator.Blocklist;

namespace Translator.Tests;

/// <summary>
/// xUnit creates one instance per test, so each test gets its own fixture directory.
/// </summary>
public class BlocklistManagerTests : IDisposable
{
    private readonly string _directory;

    public BlocklistManagerTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "translator-blocklist-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private BlocklistManager Load(string fileName, params string[] lines)
    {
        File.WriteAllLines(Path.Combine(_directory, fileName), lines);
        return new BlocklistManager(_directory);
    }

    [Fact]
    public void Constructor_BlankLineInList_DoesNotThrow()
    {
        // Regression: the upstream lists gained a blank line after their header, and
        // Split(' ')[1] threw IndexOutOfRangeException on it, which stopped the app starting.
        var exception = Record.Exception(() => Load("ads.txt", "# header", "", "0.0.0.0 example.com"));

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_BlankLineInList_StillLoadsTheRestOfTheFile()
    {
        var manager = Load("ads.txt", "# header", "", "0.0.0.0 example.com");

        Assert.NotNull(manager.TryBlock("https://example.com/"));
    }

    [Theory]
    [InlineData("0.0.0.0 example.com")]      // hosts format, as ads.txt and tracking.txt ship
    [InlineData("0.0.0.0\texample.com")]     // tab separated
    [InlineData("0.0.0.0    example.com")]   // padded with several spaces
    [InlineData("  0.0.0.0 example.com  ")]  // surrounding whitespace
    [InlineData("example.com")]              // bare domain, as my.txt holds
    public void TryBlock_SupportedLineFormat_BlocksTheDomain(string line)
    {
        var manager = Load("list.txt", line);

        Assert.NotNull(manager.TryBlock("https://example.com/"));
    }

    [Theory]
    [InlineData("0.0.0.0 EXAMPLE.COM")]
    [InlineData("0.0.0.0 Example.Com")]
    [InlineData("Example.Com")]
    public void TryBlock_EntryWithMixedCase_StillBlocks(string line)
    {
        // Uri lowercases the host, so a hand-typed capital in my.txt would otherwise
        // never match anything.
        var manager = Load("list.txt", line);

        Assert.NotNull(manager.TryBlock("https://example.com/"));
    }

    [Fact]
    public void TryBlock_MixedCaseUrl_IsBlocked()
    {
        var manager = Load("ads.txt", "0.0.0.0 example.com");

        Assert.NotNull(manager.TryBlock("https://EXAMPLE.CoM/Path"));
    }

    [Fact]
    public void TryBlock_CommentedDomain_IsNotBlocked()
    {
        var manager = Load("ads.txt", "# 0.0.0.0 example.com");

        Assert.Null(manager.TryBlock("https://example.com/"));
    }

    [Fact]
    public void TryBlock_UnlistedDomain_IsNotBlocked()
    {
        var manager = Load("ads.txt", "0.0.0.0 example.com");

        Assert.Null(manager.TryBlock("https://oxfordlearnersdictionaries.com/"));
    }

    [Fact]
    public void TryBlock_SubdomainOfBlockedDomain_IsBlocked()
    {
        var manager = Load("ads.txt", "0.0.0.0 example.com");

        Assert.NotNull(manager.TryBlock("https://ads.tracker.example.com/pixel.gif"));
    }

    [Fact]
    public void TryBlock_BlockedDomain_Returns404NotFound()
    {
        var manager = Load("ads.txt", "0.0.0.0 example.com");

        var response = manager.TryBlock("https://example.com/");

        Assert.Equal(new BlockResponse(404, "Not found"), response);
    }

    [Fact]
    public void Constructor_SeveralFiles_LoadsAllOfThem()
    {
        File.WriteAllLines(Path.Combine(_directory, "ads.txt"), ["0.0.0.0 ads.example"]);
        File.WriteAllLines(Path.Combine(_directory, "tracking.txt"), ["0.0.0.0 tracking.example"]);
        File.WriteAllLines(Path.Combine(_directory, "my.txt"), ["mine.example"]);

        var manager = new BlocklistManager(_directory);

        Assert.NotNull(manager.TryBlock("https://ads.example/"));
        Assert.NotNull(manager.TryBlock("https://tracking.example/"));
        Assert.NotNull(manager.TryBlock("https://mine.example/"));
    }

    [Fact]
    public void TryBlock_AboutBlank_IsNotBlocked()
    {
        // The window navigates to about:blank whenever it hides, so this runs constantly.
        var manager = Load("ads.txt", "0.0.0.0 example.com");

        Assert.Null(manager.TryBlock("about:blank"));
    }

    [Theory]
    [InlineData("https://ads.example.com/pixel.gif")]  // the entry itself
    [InlineData("https://eu.ads.example.com/")]        // a subdomain of it
    [InlineData("https://a.b.c.ads.example.com/")]     // several levels below it
    public void TryBlock_EntryWithThreeLabels_BlocksThatHostAndEverythingUnderIt(string url)
    {
        // 127,692 of the 235,901 entries in ads.txt have three or more labels. Before suffix
        // walking, every one of them was unreachable.
        var manager = Load("ads.txt", "0.0.0.0 ads.example.com");

        Assert.NotNull(manager.TryBlock(url));
    }

    [Fact]
    public void TryBlock_EntryWithThreeLabels_DoesNotBlockItsParentDomain()
    {
        // Blocking ads.example.com must not take out example.com itself.
        var manager = Load("ads.txt", "0.0.0.0 ads.example.com");

        Assert.Null(manager.TryBlock("https://example.com/"));
    }

    [Theory]
    [InlineData("https://notexample.com/")]
    [InlineData("https://myexample.com/")]
    public void TryBlock_HostMerelyEndingWithAnEntry_IsNotBlocked(string url)
    {
        // Guards against implementing the suffix walk with EndsWith: matching must only ever
        // drop whole labels, so notexample.com survives an example.com entry.
        var manager = Load("ads.txt", "0.0.0.0 example.com");

        Assert.Null(manager.TryBlock(url));
    }
}
