namespace Translator.Configuration;

public class SearchEngine
{
    public string Name { get; set; }
    
    public string UrlTemplate { get; set; }
    
    public string IconFileName { get; set; }
    
    /// <summary>
    /// CSS selectors for the parts of the engine's pages to hide, such as the site's header and ad slots.
    /// </summary>
    public string[] Hide { get; set; }
    
    public string AutoSearchRegex { get; set; }
}