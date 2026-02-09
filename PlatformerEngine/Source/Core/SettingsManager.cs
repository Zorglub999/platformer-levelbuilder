using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace PlatformerEngine.Source.Core
{
    public struct SettingsData
    {
        public int ResolutionWidth { get; set; }
        public int ResolutionHeight { get; set; }
        public bool IsVSync { get; set; }
        public bool IsFullscreen { get; set; }
        public float MasterVolume { get; set; }
    }

    public static class SettingsManager
    {
        private static string SettingsPath = "settings.json";
        public static SettingsData CurrentSettings { get; private set; }

        public static void Initialize()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    string json = File.ReadAllText(SettingsPath);
                    CurrentSettings = JsonSerializer.Deserialize<SettingsData>(json);
                }
                catch
                {
                    SetDefaults();
                }
            }
            else
            {
                SetDefaults();
            }
        }

        private static void SetDefaults()
        {
            CurrentSettings = new SettingsData
            {
                ResolutionWidth = 1280,
                ResolutionHeight = 720,
                IsVSync = true,
                IsFullscreen = false,
                MasterVolume = 1.0f
            };
        }

        public static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public static void Apply(GraphicsDeviceManager graphics)
        {
            graphics.PreferredBackBufferWidth = CurrentSettings.ResolutionWidth;
            graphics.PreferredBackBufferHeight = CurrentSettings.ResolutionHeight;
            graphics.SynchronizeWithVerticalRetrace = CurrentSettings.IsVSync;
            graphics.IsFullScreen = CurrentSettings.IsFullscreen;
            graphics.ApplyChanges();
        }

        public static void SetResolution(int width, int height)
        {
            var s = CurrentSettings;
            s.ResolutionWidth = width;
            s.ResolutionHeight = height;
            CurrentSettings = s;
        }

        public static void SetVSync(bool enabled)
        {
            var s = CurrentSettings;
            s.IsVSync = enabled;
            CurrentSettings = s;
        }

        public static void SetFullscreen(bool enabled)
        {
            var s = CurrentSettings;
            s.IsFullscreen = enabled;
            CurrentSettings = s;
        }

        public static void SetVolume(float volume)
        {
            var s = CurrentSettings;
            s.MasterVolume = MathHelper.Clamp(volume, 0f, 1f);
            CurrentSettings = s;
        }
    }
}
