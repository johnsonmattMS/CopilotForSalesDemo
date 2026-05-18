using System.IO;
using System.Text.Json;
using System.Windows;

namespace C4STranscriptPlayer;

public sealed class WindowSettingsService
{
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "C4STranscriptPlayer",
        "window-settings.json");

    public void Apply(Window window)
    {
        var settings = Load();
        if (settings == null) return;

        window.Width = Math.Max(window.MinWidth, settings.Width);
        window.Height = Math.Max(window.MinHeight, settings.Height);

        if (IsVisibleOnAnyScreen(settings.Left, settings.Top, settings.Width, settings.Height))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = settings.Left;
            window.Top = settings.Top;
        }

        if (settings.IsMaximized)
        {
            window.Loaded += (_, _) => window.WindowState = WindowState.Maximized;
        }
    }

    public PlayerPreferences LoadPlayerPreferences()
    {
        var settings = Load();
        if (settings == null) return new PlayerPreferences();

        return new PlayerPreferences
        {
            VoicemeeterBananaPath = settings.VoicemeeterBananaPath,
            RoutingPreset = settings.RoutingPreset,
            PlaybackMode = settings.PlaybackMode,
            SellerOutputDeviceName = settings.SellerOutputDeviceName,
            CustomerOutputDeviceName = settings.CustomerOutputDeviceName,
            SellerVoiceName = settings.SellerVoiceName,
            CustomerVoiceName = settings.CustomerVoiceName,
            VoiceRate = settings.VoiceRate,
            StartDelaySeconds = settings.StartDelaySeconds,
            SpeakSpeakerCues = settings.SpeakSpeakerCues
        };
    }

    public void SavePlayerPreferences(PlayerPreferences preferences)
    {
        var settings = Load() ?? new WindowSettings();
        ApplyPlayerPreferences(settings, preferences);
        Save(settings);
    }

    public void Save(Window window, PlayerPreferences preferences)
    {
        var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
        var settings = Load() ?? new WindowSettings();
        settings.Width = Math.Max(window.MinWidth, bounds.Width);
        settings.Height = Math.Max(window.MinHeight, bounds.Height);
        settings.Left = bounds.Left;
        settings.Top = bounds.Top;
        settings.IsMaximized = window.WindowState == WindowState.Maximized;
        ApplyPlayerPreferences(settings, preferences);

        Save(settings);
    }

    private static void ApplyPlayerPreferences(WindowSettings settings, PlayerPreferences preferences)
    {
        settings.VoicemeeterBananaPath = string.IsNullOrWhiteSpace(preferences.VoicemeeterBananaPath) ? settings.VoicemeeterBananaPath : preferences.VoicemeeterBananaPath;
        settings.RoutingPreset = preferences.RoutingPreset;
        settings.PlaybackMode = preferences.PlaybackMode;
        settings.SellerOutputDeviceName = preferences.SellerOutputDeviceName;
        settings.CustomerOutputDeviceName = preferences.CustomerOutputDeviceName;
        settings.SellerVoiceName = preferences.SellerVoiceName;
        settings.CustomerVoiceName = preferences.CustomerVoiceName;
        settings.VoiceRate = preferences.VoiceRate;
        settings.StartDelaySeconds = preferences.StartDelaySeconds;
        settings.SpeakSpeakerCues = preferences.SpeakSpeakerCues;
    }

    private void Save(WindowSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private WindowSettings? Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return null;
            return JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(_settingsPath));
        }
        catch
        {
            return null;
        }
    }

    private static bool IsVisibleOnAnyScreen(double left, double top, double width, double height)
    {
        var windowRect = new Rect(left, top, Math.Max(100, width), Math.Max(100, height));
        var virtualScreen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        return virtualScreen.IntersectsWith(windowRect);
    }

    private sealed class WindowSettings
    {
        public double Width { get; set; } = 1360;
        public double Height { get; set; } = 1000;
        public double Left { get; set; }
        public double Top { get; set; }
        public bool IsMaximized { get; set; }
        public string? VoicemeeterBananaPath { get; set; }
        public string? RoutingPreset { get; set; }
        public PlaybackMode? PlaybackMode { get; set; }
        public string? SellerOutputDeviceName { get; set; }
        public string? CustomerOutputDeviceName { get; set; }
        public string? SellerVoiceName { get; set; }
        public string? CustomerVoiceName { get; set; }
        public int? VoiceRate { get; set; }
        public int? StartDelaySeconds { get; set; }
        public bool? SpeakSpeakerCues { get; set; }
    }
}

public sealed record PlayerPreferences
{
    public string? VoicemeeterBananaPath { get; init; }
    public string? RoutingPreset { get; init; }
    public PlaybackMode? PlaybackMode { get; init; }
    public string? SellerOutputDeviceName { get; init; }
    public string? CustomerOutputDeviceName { get; init; }
    public string? SellerVoiceName { get; init; }
    public string? CustomerVoiceName { get; init; }
    public int? VoiceRate { get; init; }
    public int? StartDelaySeconds { get; init; }
    public bool? SpeakSpeakerCues { get; init; }
}
