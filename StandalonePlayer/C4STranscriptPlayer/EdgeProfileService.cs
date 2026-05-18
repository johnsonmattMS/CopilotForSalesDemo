using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using Microsoft.Identity.Client.Extensibility;

namespace C4STranscriptPlayer;

public sealed record EdgeProfile(string DisplayName, string ProfileDirectory, bool IsDefault)
{
    public override string ToString()
    {
        var suffix = IsDefault ? " (default Edge profile)" : string.Empty;
        return $"{DisplayName} [{ProfileDirectory}]{suffix}";
    }
}

public sealed class EdgeProfileService
{
    public IReadOnlyList<EdgeProfile> GetProfiles()
    {
        var userDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data");
        var localStatePath = Path.Combine(userDataPath, "Local State");
        if (!File.Exists(localStatePath)) return Array.Empty<EdgeProfile>();

        using var document = JsonDocument.Parse(File.ReadAllText(localStatePath));
        if (!document.RootElement.TryGetProperty("profile", out var profileElement)) return Array.Empty<EdgeProfile>();

        var lastUsed = profileElement.TryGetProperty("last_used", out var lastUsedElement)
            ? lastUsedElement.GetString()
            : null;

        if (!profileElement.TryGetProperty("info_cache", out var infoCacheElement)) return Array.Empty<EdgeProfile>();

        var profiles = new List<EdgeProfile>();
        foreach (var profile in infoCacheElement.EnumerateObject())
        {
            var directory = profile.Name;
            var name = GetProfileString(profile.Value, "name")
                ?? GetProfileString(profile.Value, "gaia_name")
                ?? GetProfileString(profile.Value, "user_name")
                ?? directory;
            var userName = GetProfileString(profile.Value, "user_name");
            var displayName = string.IsNullOrWhiteSpace(userName) || name.Contains(userName, StringComparison.OrdinalIgnoreCase)
                ? name
                : $"{name} - {userName}";

            profiles.Add(new EdgeProfile(displayName, directory, string.Equals(directory, lastUsed, StringComparison.OrdinalIgnoreCase)));
        }

        return profiles
            .OrderByDescending(profile => profile.IsDefault)
            .ThenBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GetProfileString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
    }
}

public sealed class EdgeProfileWebUi : ICustomWebUi
{
    private readonly EdgeProfile _profile;

    public EdgeProfileWebUi(EdgeProfile profile)
    {
        _profile = profile;
    }

    public async Task<Uri> AcquireAuthorizationCodeAsync(Uri authorizationUri, Uri redirectUri, CancellationToken cancellationToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(BuildListenerPrefix(redirectUri));
        listener.Start();

        LaunchEdge(authorizationUri);

        using var registration = cancellationToken.Register(() =>
        {
            try { listener.Stop(); }
            catch { }
        });

        try
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            var responseHtml = "<html><body style='font-family:Segoe UI;margin:32px'><h2>Authentication complete</h2><p>You can return to C4S Transcript Player.</p></body></html>";
            var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, cancellationToken);
            context.Response.Close();
            return context.Request.Url ?? throw new InvalidOperationException("The authentication callback did not include a URL.");
        }
        finally
        {
            listener.Stop();
        }
    }

    private void LaunchEdge(Uri authorizationUri)
    {
        var edgePath = FindEdgePath();
        var arguments = $"--profile-directory=\"{_profile.ProfileDirectory}\" --new-window \"{authorizationUri.AbsoluteUri}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = edgePath,
            Arguments = arguments,
            UseShellExecute = false
        });
    }

    private static string BuildListenerPrefix(Uri redirectUri)
    {
        var builder = new UriBuilder(redirectUri)
        {
            Path = redirectUri.AbsolutePath.TrimEnd('/') + "/"
        };
        return builder.Uri.AbsoluteUri;
    }

    private static string FindEdgePath()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? "msedge.exe";
    }
}
