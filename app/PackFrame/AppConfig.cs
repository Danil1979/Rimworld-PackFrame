using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PackFrame;

public sealed class AppConfig
{
    public string? Language { get; set; }
    public string? GamePath { get; set; }
    public string? OfficialConfigPath { get; set; }
    public string? ProfileFolder1 { get; set; }
    public string? ProfileFolder2 { get; set; }
    public bool ShowSecondaryPane { get; set; }
    public List<string> ProfileOrder { get; set; } = new();
    public string? ModpackIndexUrl { get; set; }
    public string? BackgroundColor { get; set; }
    public string? OssEndpoint { get; set; }
    public string? OssBucket { get; set; }
    public string? OssPrefix { get; set; }
    public string? OssAccessKeyId { get; set; }
    public string? OssAccessKeySecret { get; set; }
    public bool OssEnabled { get; set; } = true;

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "packframe.json");

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize(
                json,
                PackFrameJsonContext.Default.AppConfig
            ) ?? new AppConfig();
        }
        catch (Exception exception)
        {
            AppLog.Error("Read app config: " + ConfigPath, exception);
            return new AppConfig();
        }
    }

    public void Save()
    {
        TrySave();
    }

    public bool TrySave() => TrySave(ConfigPath);

    internal AppConfig Copy() => (AppConfig)MemberwiseClone();

    internal bool TrySave(string path)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                this,
                PackFrameJsonContext.Default.AppConfig
            );
            FileTransactions.WriteAtomic(path, temporary => File.WriteAllText(temporary, json));
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Error("Save app config: " + path, exception);
            return false;
        }
    }
}
