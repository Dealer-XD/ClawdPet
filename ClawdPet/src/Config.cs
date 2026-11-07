using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace ClawdPet
{
    public class Config
    {
        public bool Show { get; set; } = true;
        public int Size { get; set; } = 1;
        public int Color { get; set; } = 0;
        public int Jump { get; set; } = 1;
        public bool Gravity { get; set; } = true;
        public bool Wander { get; set; } = true;
        public bool Phrases { get; set; } = true;
        public int Sleep { get; set; } = 1;
        public bool Poop { get; set; } = true;
        public bool Dance { get; set; } = true;
        public bool AppEvents { get; set; } = true;
        public bool RideWindows { get; set; } = true;
        public bool ShakeWindows { get; set; } = true;
        public bool HideFullscreen { get; set; } = true;
        public bool EatIcons { get; set; } = true;

        public bool AiEnabled { get; set; } = false;
        public string AiKey { get; set; } = "";
        public int AiProvider { get; set; } = 0;
        public string AiUrl { get; set; } = "";
        public string AiModel { get; set; } = "";
        public int AiChat { get; set; } = 1;
        public bool AiMoods { get; set; } = true;
        public bool AiMove { get; set; } = true;
        public string Ai2Url { get; set; } = "";
        public string Ai2Key { get; set; } = "";
        public string Ai2Model { get; set; } = "";
        public bool AiTitles { get; set; } = false;
        public string AiPersona { get; set; } = "";
        public bool AiFallback { get; set; } = true;

        public bool BrainEnabled { get; set; } = true;
        public string BrainName { get; set; } = "";
        public string BrainAbout { get; set; } = "";
        public bool BrainLearn { get; set; } = true;
        public bool BrainShare { get; set; } = true;

        public int PosX { get; set; } = -1;
        public int PosY { get; set; } = -1;
        public int PetsCount { get; set; }
        public int EatenCount { get; set; }
        public int PoopsCleaned { get; set; }
        public int PoopMeter { get; set; }

        public static string Dir
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClawdPet");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        static string FilePath => Path.Combine(Dir, "config.json");
        static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

        public static Config Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath)) ?? new Config();
            }
            catch (Exception e) { Log.Write("config load: " + e.Message); }
            return new Config();
        }

        public void Save()
        {
            try
            {
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, Opts));
                File.Move(tmp, FilePath, true);
            }
            catch (Exception e) { Log.Write("config save: " + e.Message); }
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool Autostart
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                    return key?.GetValue("ClawdPet") != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                    if (value) key.SetValue("ClawdPet", "\"" + Environment.ProcessPath + "\"");
                    else key.DeleteValue("ClawdPet", false);
                }
                catch (Exception e) { Log.Write("autostart: " + e.Message); }
            }
        }
    }

    public static class Log
    {
        static readonly object Lock = new();
        public static void Write(string msg)
        {
            try
            {
                lock (Lock)
                {
                    var path = Path.Combine(Config.Dir, "clawd.log");
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > 512 * 1024) info.Delete();
                    File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
                }
            }
            catch { }
        }
    }
}
