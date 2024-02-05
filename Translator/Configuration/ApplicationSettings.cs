namespace Translator.Configuration;

public class ApplicationSettings
{
    public string TestSetting { get; set; }
    
    public Subsettings Subsettings { get; set; }
}

public class Subsettings
{
    public string TestSubsetting { get; set; }
}