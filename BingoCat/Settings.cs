using System.Text.Json;
using System.IO;

namespace BingoCat;

public class Settings
{
    public int XOffset { get; set; } = 3000;
    public int YOffset { get; set; } = -120;
    public string TriggerMode { get; set; } = "sound"; // or "sound" or input
    public string ImageSet { get; set; } = "default"; // Name of the folder containing the cat images
    public float ScaleFactor { get; set; } = .5f;    // Scale the images up or down

    public static Settings Load()
    {
        string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        if (File.Exists(settingsPath))
        {
            try
            {
                string json = File.ReadAllText(settingsPath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            catch { }
        }
        return new Settings();
    }
}
