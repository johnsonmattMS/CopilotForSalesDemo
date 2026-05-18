using System.Diagnostics;
using System.IO;
using System.Windows;

namespace C4STranscriptPlayer;

public partial class MainWindow : Window
{
    private readonly TranscriptGenerator _generator = new();
    private readonly AudioPlaybackService _audio = new();
    private readonly DataverseAppointmentService _dataverse = new();
    private readonly EdgeProfileService _edgeProfiles = new();
    private readonly WindowSettingsService _windowSettings = new();
    private TranscriptResult? _currentTranscript;

    public MainWindow()
    {
        InitializeComponent();
        _windowSettings.Apply(this);
        Closing += (_, _) => SaveWindowSettings();
        InitializeControls();
        ApplyContext(new AppointmentContext());
        GenerateTranscript();
    }

    private void InitializeControls()
    {
        EnvironmentUrlBox.Text = "https://org3c71a034.crm4.dynamics.com/";
        VoicemeeterPathBox.Text = _windowSettings.LoadVoicemeeterBananaPath() ?? GetDefaultVoicemeeterBananaPath();
        ThemeBox.ItemsSource = _generator.ThemeOptions;
        ThemeBox.SelectedValue = "renewal-risk";
        ToneBox.ItemsSource = new[] { "balanced", "positive", "tense", "recovery" };
        ToneBox.SelectedItem = "balanced";
        LengthBox.ItemsSource = new[] { "short", "medium", "detailed" };
        LengthBox.SelectedItem = "medium";
        PlaybackModeBox.ItemsSource = new[]
        {
            new ComboOption<PlaybackMode>("Both speakers", PlaybackMode.Both),
            new ComboOption<PlaybackMode>("Seller only", PlaybackMode.SellerOnly),
            new ComboOption<PlaybackMode>("Customer only", PlaybackMode.CustomerOnly)
        };
        PlaybackModeBox.SelectedIndex = 0;
        RoutingPresetBox.ItemsSource = new[]
        {
            new ComboOption<string>("Voicemeeter free two-speaker setup", "voicemeeter"),
            new ComboOption<string>("Free VB-CABLE plus speakers/headphones", "single-cable"),
            new ComboOption<string>("VB-CABLE A+B, if installed", "vb-ab"),
            new ComboOption<string>("Manual device selection", "manual")
        };
        RoutingPresetBox.SelectedIndex = 0;
        VoiceSpeedBox.ItemsSource = new[]
        {
            new ComboOption<int>("1x - natural", 0),
            new ComboOption<int>("1.5x - quick", 3),
            new ComboOption<int>("2x - demo default", 5),
            new ComboOption<int>("2.5x - fast", 7),
            new ComboOption<int>("3x - very fast", 9)
        };
        VoiceSpeedBox.SelectedIndex = 2;
        StartDelayBox.ItemsSource = new[]
        {
            new ComboOption<int>("No delay", 0),
            new ComboOption<int>("3 seconds", 3),
            new ComboOption<int>("5 seconds", 5),
            new ComboOption<int>("10 seconds", 10)
        };
        StartDelayBox.SelectedIndex = 0;
        ScenarioNotesBox.Text = "The customer account is interested but concerned about delivery confidence, stakeholder adoption, and proving value quickly.";
        RefreshEdgeProfiles();
        RefreshAudioSelectors();
        ApplyRoutingPreset(showStatus: false);
    }

    private void RefreshEdgeProfiles()
    {
        var profiles = _edgeProfiles.GetProfiles();
        EdgeProfileBox.ItemsSource = profiles;
        EdgeProfileBox.SelectedItem = profiles.FirstOrDefault(profile => profile.IsDefault) ?? profiles.FirstOrDefault();
        if (profiles.Count == 0)
        {
            StatusText.Text = "No Edge profiles were found. Dataverse login will use the default browser prompt.";
        }
    }

    private void RefreshAudioSelectors()
    {
        var devices = _audio.GetOutputDevices();
        SellerDeviceBox.ItemsSource = devices;
        CustomerDeviceBox.ItemsSource = devices;
        SellerDeviceBox.SelectedItem = FindPreferredDevice(devices, "CABLE-A Input") ?? devices.FirstOrDefault();
        CustomerDeviceBox.SelectedItem = FindPreferredDevice(devices, "CABLE-B Input") ?? devices.Skip(1).FirstOrDefault() ?? devices.FirstOrDefault();

        var voices = _audio.GetVoices();
        SellerVoiceBox.ItemsSource = voices;
        CustomerVoiceBox.ItemsSource = voices;
        SellerVoiceBox.SelectedItem = voices.FirstOrDefault(voice => ContainsAny(voice.Name, "David", "Guy", "Mark", "George", "Ryan")) ?? voices.FirstOrDefault();
        CustomerVoiceBox.SelectedItem = voices.FirstOrDefault(voice => !Equals(voice, SellerVoiceBox.SelectedItem) && ContainsAny(voice.Name, "Zira", "Jenny", "Aria", "Sonia", "Hazel", "Susan")) ?? voices.FirstOrDefault(voice => !Equals(voice, SellerVoiceBox.SelectedItem)) ?? voices.FirstOrDefault();
    }

    private void ApplyRoutingPreset(bool showStatus = true)
    {
        var devices = SellerDeviceBox.ItemsSource?.Cast<AudioOutputDevice>().ToList() ?? new List<AudioOutputDevice>();
        var preset = ((ComboOption<string>?)RoutingPresetBox.SelectedItem)?.Value ?? "manual";

        switch (preset)
        {
            case "voicemeeter":
                SellerDeviceBox.SelectedItem = FindPreferredDevice(devices, "VoiceMeeter Input", "Voicemeeter Input")
                    ?? FindPreferredDevice(devices, "VoiceMeeter VAIO", "Voicemeeter VAIO")
                    ?? SellerDeviceBox.SelectedItem;
                CustomerDeviceBox.SelectedItem = FindPreferredDevice(devices, "VoiceMeeter Aux Input", "Voicemeeter AUX Input", "VoiceMeeter AUX")
                    ?? FindPreferredDevice(devices, "VoiceMeeter VAIO3", "Voicemeeter VAIO3")
                    ?? CustomerDeviceBox.SelectedItem;
                StatusText.Text = "Preset applied. In Teams, use Voicemeeter Output for seller and Voicemeeter AUX Output for customer.";
                break;
            case "single-cable":
                SellerDeviceBox.SelectedItem = FindPreferredDevice(devices, "CABLE Input", "VB-Audio Virtual Cable")
                    ?? SellerDeviceBox.SelectedItem;
                CustomerDeviceBox.SelectedItem = devices.FirstOrDefault(device => device.DeviceNumber == -1)
                    ?? devices.FirstOrDefault(device => !ContainsAny(device.Name, "CABLE", "VoiceMeeter", "Voicemeeter"))
                    ?? CustomerDeviceBox.SelectedItem;
                StatusText.Text = "Preset applied. Use VB-CABLE Output as one Teams mic and capture the second side from speakers/headphones or another participant.";
                break;
            case "vb-ab":
                SellerDeviceBox.SelectedItem = FindPreferredDevice(devices, "CABLE-A Input")
                    ?? SellerDeviceBox.SelectedItem;
                CustomerDeviceBox.SelectedItem = FindPreferredDevice(devices, "CABLE-B Input")
                    ?? CustomerDeviceBox.SelectedItem;
                StatusText.Text = "Preset applied. In Teams, use CABLE-A Output for seller and CABLE-B Output for customer.";
                break;
            default:
                StatusText.Text = "Manual routing selected.";
                break;
        }

        if (!showStatus) StatusText.Text = "Transcript generated.";
    }

    private static AudioOutputDevice? FindPreferredDevice(IEnumerable<AudioOutputDevice> devices, params string[] preferredNames)
    {
        return devices.FirstOrDefault(device => preferredNames.Any(preferredName => device.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ContainsAny(string value, params string[] parts)
    {
        return parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyContext(AppointmentContext context)
    {
        AppointmentIdBox.Text = context.AppointmentId ?? AppointmentIdBox.Text;
        AppointmentSubjectBox.Text = context.Subject;
        MeetingDateBox.Text = context.Start.ToString("g");
        DurationMinutesBox.Text = context.DurationMinutes.ToString();
        RecordNameBox.Text = context.Regarding.Name;
        CustomerAccountBox.Text = context.CustomerAccount.Name;
        SellerNameBox.Text = context.Seller.Name;
        CustomerNameBox.Text = context.CustomerContact.Name;
    }

    private TranscriptInput BuildInput()
    {
        return new TranscriptInput
        {
            AppointmentSubject = BlankAs(AppointmentSubjectBox.Text, ""),
            RecordName = BlankAs(RecordNameBox.Text, "Selected CRM record"),
            CustomerAccountName = BlankAs(CustomerAccountBox.Text, "the customer account"),
            SellerName = BlankAs(SellerNameBox.Text, "Seller"),
            CustomerName = BlankAs(CustomerNameBox.Text, "Customer"),
            Theme = ThemeBox.SelectedValue as string ?? "renewal-risk",
            Tone = ToneBox.SelectedItem as string ?? "balanced",
            Length = LengthBox.SelectedItem as string ?? "medium",
            Notes = ScenarioNotesBox.Text.Trim(),
            MeetingDate = ParseMeetingDate(),
            DurationMinutes = ParseDurationMinutes(),
            Sentiment = "Neutral"
        };
    }

    private DateTime ParseMeetingDate()
    {
        return DateTime.TryParse(MeetingDateBox.Text, out var meetingDate) ? meetingDate : DateTime.Now;
    }

    private int ParseDurationMinutes()
    {
        return int.TryParse(DurationMinutesBox.Text, out var minutes) ? Math.Max(15, minutes) : 30;
    }

    private static string BlankAs(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private void GenerateTranscript()
    {
        _currentTranscript = _generator.Generate(BuildInput());
        TitleText.Text = _currentTranscript.Title;
        SummaryText.Text = _currentTranscript.Summary;
        TranscriptBox.Text = _currentTranscript.TranscriptText;
        TasksList.ItemsSource = _currentTranscript.Tasks.Select(task => $"{task.Title}\nOwner: {task.Owner} | Due: {task.Due}").ToList();
        StatusText.Text = "Transcript generated.";
    }

    private PlaybackOptions BuildPlaybackOptions()
    {
        return new PlaybackOptions
        {
            Mode = ((ComboOption<PlaybackMode>?)PlaybackModeBox.SelectedItem)?.Value ?? PlaybackMode.Both,
            SellerDevice = SellerDeviceBox.SelectedItem as AudioOutputDevice,
            CustomerDevice = CustomerDeviceBox.SelectedItem as AudioOutputDevice,
            SellerVoice = (SellerVoiceBox.SelectedItem as SpeechVoice)?.Name,
            CustomerVoice = (CustomerVoiceBox.SelectedItem as SpeechVoice)?.Name,
            VoiceRate = ((ComboOption<int>?)VoiceSpeedBox.SelectedItem)?.Value ?? 5,
            StartDelaySeconds = ((ComboOption<int>?)StartDelayBox.SelectedItem)?.Value ?? 3,
            SpeakSpeakerCues = SpeakerCuesBox.IsChecked == true
        };
    }

    private async void LoadAppointmentButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true, "Loading appointment from Dataverse...");
        try
        {
            var context = await _dataverse.LoadAppointmentAsync(EnvironmentUrlBox.Text, AppointmentIdBox.Text, EdgeProfileBox.SelectedItem as EdgeProfile);
            ApplyContext(context);
            GenerateTranscript();
            StatusText.Text = "Appointment loaded.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Load failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UseDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyContext(new AppointmentContext());
        GenerateTranscript();
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        GenerateTranscript();
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTranscript == null) GenerateTranscript();
        if (_currentTranscript == null) return;

        SetBusy(true, "Preparing playback...");
        var progress = new Progress<string>(message => StatusText.Text = message);
        try
        {
            await _audio.PlayAsync(_currentTranscript, BuildPlaybackOptions(), progress);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Playback stopped.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Playback failed: " + ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _audio.Stop();
        StatusText.Text = "Stopping playback...";
    }

    private void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshAudioSelectors();
        ApplyRoutingPreset();
    }

    private void RefreshProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshEdgeProfiles();
        StatusText.Text = EdgeProfileBox.SelectedItem is EdgeProfile profile
            ? $"Edge profiles refreshed. Selected: {profile.DisplayName}."
            : "Edge profiles refreshed, but none were found.";
    }

    private void ApplyRoutingPresetButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyRoutingPreset();
    }

    private void VoicemeeterHelpButton_Click(object sender, RoutedEventArgs e)
    {
        var helpWindow = new VoicemeeterHelpWindow { Owner = this };
        helpWindow.Show();
    }

    private void LaunchVoicemeeterButton_Click(object sender, RoutedEventArgs e)
    {
        var path = VoicemeeterPathBox.Text.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText.Text = "Enter the Voicemeeter Banana path before launching the mixer.";
            return;
        }

        if (!File.Exists(path))
        {
            StatusText.Text = "Voicemeeter Banana was not found at that path.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            _windowSettings.SaveVoicemeeterBananaPath(path);
            StatusText.Text = "Voicemeeter Banana launch requested. Keep it running before starting playback.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Mixer launch failed: " + ex.Message;
        }
    }

    private void SaveWindowSettings()
    {
        _windowSettings.Save(this, VoicemeeterPathBox.Text.Trim().Trim('"'));
    }

    private static string GetDefaultVoicemeeterBananaPath()
    {
        var candidates = new[]
        {
            @"C:\Program Files (x86)\VB\Voicemeeter\voicemeeterpro.exe",
            @"C:\Program Files\VB\Voicemeeter\voicemeeterpro.exe"
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private void SetBusy(bool isBusy, string? message = null)
    {
        LoadAppointmentButton.IsEnabled = !isBusy;
        PlayButton.IsEnabled = !isBusy;
        GenerateButton.IsEnabled = !isBusy;
        if (!string.IsNullOrWhiteSpace(message)) StatusText.Text = message;
    }

    private sealed record ComboOption<T>(string Label, T Value)
    {
        public override string ToString() => Label;
    }
}
