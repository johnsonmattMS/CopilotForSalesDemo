using NAudio.Wave;

namespace C4STranscriptPlayer;

public sealed record LookupValue(string? Id, string Name, string EntityName);

public sealed class AppointmentContext
{
    public string? AppointmentId { get; set; }
    public string Subject { get; set; } = "Contoso ERP Modernisation";
    public DateTime Start { get; set; } = DateTime.Now.AddDays(1).Date.AddHours(10);
    public int DurationMinutes { get; set; } = 30;
    public LookupValue Regarding { get; set; } = new(null, "Contoso ERP Modernisation", "opportunity");
    public LookupValue CustomerAccount { get; set; } = new(null, "HSBC", "account");
    public LookupValue Seller { get; set; } = new(null, "Avery Howard", "systemuser");
    public LookupValue CustomerContact { get; set; } = new(null, "Morgan Lee", "contact");
}

public sealed class TranscriptInput
{
    public string AppointmentSubject { get; set; } = "";
    public string RecordName { get; set; } = "Selected CRM record";
    public string CustomerAccountName { get; set; } = "the customer account";
    public string SellerName { get; set; } = "Seller";
    public string CustomerName { get; set; } = "Customer";
    public string Theme { get; set; } = "renewal-risk";
    public string Tone { get; set; } = "balanced";
    public string Length { get; set; } = "medium";
    public string Notes { get; set; } = "The customer account is interested but concerned about delivery confidence, stakeholder adoption, and proving value quickly.";
    public DateTime MeetingDate { get; set; } = DateTime.Now.AddDays(1).Date.AddHours(10);
    public int DurationMinutes { get; set; } = 30;
    public string Sentiment { get; set; } = "Neutral";
}

public sealed class TranscriptResult
{
    public TranscriptInput Input { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string ThemeLabel { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> Points { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Decisions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Risks { get; set; } = Array.Empty<string>();
    public IReadOnlyList<FollowUpTask> Tasks { get; set; } = Array.Empty<FollowUpTask>();
    public IReadOnlyList<TranscriptCue> Cues { get; set; } = Array.Empty<TranscriptCue>();
    public string TranscriptText => string.Join(Environment.NewLine, Cues.Select(c => c.RawLine));
}

public sealed record FollowUpTask(string Title, string Owner, string Due);

public sealed record TranscriptCue(TimeSpan Offset, SpeakerRole Role, string Speaker, string Text, string RawLine);

public enum SpeakerRole
{
    Seller,
    Customer
}

public enum PlaybackMode
{
    Both,
    SellerOnly,
    CustomerOnly
}

public sealed record AudioOutputDevice(int DeviceNumber, string Name)
{
    public override string ToString() => Name;
}

public sealed record SpeechVoice(string Name, string Label, string CultureName, bool IsEnglish)
{
    public override string ToString() => Label;
}

public sealed class PlaybackOptions
{
    public PlaybackMode Mode { get; set; }
    public AudioOutputDevice? SellerDevice { get; set; }
    public AudioOutputDevice? CustomerDevice { get; set; }
    public string? SellerVoice { get; set; }
    public string? CustomerVoice { get; set; }
    public int VoiceRate { get; set; } = 5;
    public int StartDelaySeconds { get; set; } = 3;
    public bool SpeakSpeakerCues { get; set; } = true;
}
