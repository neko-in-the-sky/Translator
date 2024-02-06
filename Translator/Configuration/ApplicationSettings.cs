namespace Translator.Configuration;

public class ApplicationSettings
{
    public SearchEngine[] SearchEngines { get; set; }
    
    public string DefaultSearchEngine { get; set; }
    
    public string[] AllowedFullscreenApps { get; set; }
    
    public string Culture { get; set; }
    
    public PopupSettings Popup { get; set; }
}