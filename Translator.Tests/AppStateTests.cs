// UseWPF projects drop System.IO from the implicit usings, because System.Windows.Shapes.Path
// would collide with System.IO.Path, so it has to be imported explicitly here.
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;

namespace Translator.Tests;

/// <summary>
/// xUnit creates one instance per test, so each test gets its own directory.
/// </summary>
public class AppStateTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "translator-state-" + Guid.NewGuid().ToString("N"));

    private string StatePath => Path.Combine(_directory, "state.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private AppState Load(string? path = null) => new(path ?? StatePath, NullLogger<AppState>.Instance);

    [Fact]
    public void LastSearchEngine_Saved_ReadsBack()
    {
        Load().SetLastSearchEngine("Google Translate");

        Assert.Equal("Google Translate", Load().LastSearchEngine);
        Assert.False(File.Exists(StatePath + ".tmp"));
    }

    [Fact]
    public void LastSearchEngine_NoFile_IsNull()
    {
        Assert.Null(Load().LastSearchEngine);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "LastSearchEngine": 5 }""")]
    [InlineData("""{ "Other": "Oxford" }""")]
    public void LastSearchEngine_UnusableFile_IsNull(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(StatePath, content);

        Assert.Null(Load().LastSearchEngine);
    }

    [Fact]
    public void SetLastSearchEngine_FolderCannotBeCreated_DoesNotThrow()
    {
        // A file where the folder should be makes CreateDirectory fail with an IOException.
        Directory.CreateDirectory(_directory);
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "");
        var state = Load(Path.Combine(blocker, "state.json"));

        var exception = Record.Exception(() => state.SetLastSearchEngine("Oxford"));

        Assert.Null(exception);
        Assert.Equal("Oxford", state.LastSearchEngine);
    }
}
