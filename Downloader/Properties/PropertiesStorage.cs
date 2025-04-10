using Newtonsoft.Json;

namespace MusicApiDownloader.Properties;

internal class PropertiesStorage {

    public static PropertiesStorage Instance {
        get {
            _instance ??= LoadOrCreateStorage();
            return _instance;
        }
    }
    
    public string? ApiToken { get; set; }

    public string? SavePath { get; set; }

    static PropertiesStorage() {
        _propertiesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constants.ApplicationName, Constants.UserDataFileName);
    }
    
    [JsonConstructor]
    private PropertiesStorage() {
        
    }

    private static PropertiesStorage LoadOrCreateStorage() {
        string? propertiesContent;
        try {
            propertiesContent = File.Exists(_propertiesPath) ? File.ReadAllText(_propertiesPath) : null;
        } catch {
            propertiesContent = null;
        }
        if (propertiesContent is null) {
            return new();
        }
        return JsonConvert.DeserializeObject<PropertiesStorage>(propertiesContent) ?? new();
    }

    internal void Save() {
        var content = JsonConvert.SerializeObject(this);
        if (!Directory.Exists(_propertiesPath)) {
            Directory.CreateDirectory(Path.GetDirectoryName(_propertiesPath)!);
        }
        File.WriteAllText(_propertiesPath, content);
    }
    
    private static readonly string _propertiesPath;
    private static PropertiesStorage? _instance;

}
