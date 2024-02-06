namespace Translator.Configuration;

public class SearchEngine
{
    public string Name { get; set; }
    
    public string UrlTemplate { get; set; }
    
    public string IconFileName { get; set; }
    
    public string JsFileName { get; set; }
    
    public string AutoSearchRegex { get; set; }
}