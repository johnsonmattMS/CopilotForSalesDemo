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

    public string? LoadVoicemeeterBananaPath()
    {
        return Load()?.VoicemeeterBananaPath;
    }

    public void SaveVoicemeeterBananaPath(string path)
    {
        var settings = Load() ?? new WindowSettings();
        settings.VoicemeeterBananaPath = path;
        Save(settings);
    }

    public void Save(Window window, string? voicemeeterBananaPath)
    {
        var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
        var settings = Load() ?? new WindowSettings();
        settings.Width = Math.Max(window.MinWidth, bounds.Width);
        settings.Height = Math.Max(window.MinHeight, bounds.Height);
        settings.Left = bounds.Left;
        settings.Top = bounds.Top;
        settings.IsMaximized = window.WindowState == WindowState.Maximized;
        settings.VoicemeeterBananaPath = string.IsNullOrWhiteSpace(voicemeeterBananaPath) ? settings.VoicemeeterBananaPath : voicemeeterBananaPath;

        Save(settings);
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
    }
}
