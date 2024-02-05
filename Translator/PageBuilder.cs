using System.IO;

namespace Translator;

public class PageBuilder
{
    private const string BodyPlaceHolder = "{BODY}";
    private const string HeaderPlaceHolder = "{HEADER}";
    private readonly string _infoPageTemplate;
    private readonly string _errorPageTemplate;
    
    public PageBuilder()
    {
        _infoPageTemplate = File.ReadAllText(Path.Combine("Templates", "info_template.html"));
        _errorPageTemplate = File.ReadAllText(Path.Combine("Templates", "error_template.html"));
    }
    
    public string MakeInfoPage(string result)
    {
        var newPage = _infoPageTemplate
            .Replace(BodyPlaceHolder, result)
            .Replace(HeaderPlaceHolder, string.Empty);
        return newPage;
    }

    public string MakeErrorPage(string error)
    {
        var newPage = _errorPageTemplate.Replace(BodyPlaceHolder, error);
        newPage = newPage.Replace(HeaderPlaceHolder, Properties.Resources.Error_CannotTranslate);
        return newPage;
    }
}