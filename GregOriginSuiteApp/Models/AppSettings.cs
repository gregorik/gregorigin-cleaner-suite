using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using GregOriginSuiteApp.Services;

namespace GregOriginSuiteApp.Models
{
    public class AppSettings
    {
        public bool IsDarkMode { get; set; } = false;
        public bool SkipConfirmations { get; set; } = false;

        private static string GetSettingsPath()
        {
            return Path.Combine(AppPaths.AppDataDirectory, "settings.json");
        }

        public static AppSettings Load()
        {
            try
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to load GregOrigin settings: " + ex.Message);
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string path = GetSettingsPath();
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to save GregOrigin settings: " + ex.Message);
            }
        }
    }
}
