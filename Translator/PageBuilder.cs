using System.IO;

namespace Translator;

public class PageBuilder
{
    private const string BodyPlaceHolder = "{BODY}";
    private readonly string _infoPageTemplate = File.ReadAllText(Path.Combine("templates", "info_template.html"));

    public string MakeInfoPage(string result) => _infoPageTemplate.Replace(BodyPlaceHolder, result);
}