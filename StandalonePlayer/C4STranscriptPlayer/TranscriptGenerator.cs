namespace C4STranscriptPlayer;

public sealed class TranscriptGenerator
{
    private sealed record Theme(string Label, string Subject, string[] Points, string[] Risks, string[] Tasks);

    private readonly Dictionary<string, Theme> _themes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["renewal-risk"] = new(
            "Renewal risk",
            "Renewal health review",
            ["The customer wants clearer evidence of value before renewal.", "Adoption is uneven across business units.", "A senior sponsor needs a concise risk and value brief."],
            ["Renewal decision may slip without quantified benefits.", "Competing priorities could reduce stakeholder engagement."],
            ["Send value realisation summary", "Schedule sponsor alignment call", "Share adoption improvement plan"]),
        ["pricing-objection"] = new(
            "Pricing objection",
            "Commercial review",
            ["The customer challenged the proposed pricing model.", "The seller reframed the proposal around risk reduction and speed to value.", "Procurement needs a clearer breakdown of implementation phases."],
            ["Pricing pressure could reduce scope.", "Procurement may benchmark against a lower-cost competitor."],
            ["Send phased pricing option", "Prepare value comparison", "Book procurement follow-up"]),
        ["project-delay"] = new(
            "Project delay",
            "Delivery confidence check-in",
            ["The customer raised concerns about timeline confidence.", "The seller acknowledged the delay and proposed tighter governance.", "Both sides agreed to weekly checkpoint reporting."],
            ["Delayed dependency decisions may affect go-live.", "Stakeholder confidence needs rebuilding."],
            ["Share revised delivery plan", "Confirm dependency owners", "Create weekly checkpoint cadence"]),
        ["cross-sell"] = new(
            "Cross-sell discovery",
            "Expansion discovery call",
            ["The customer described adjacent needs in service operations.", "The seller connected the current deployment to a broader transformation roadmap.", "There is appetite for a short discovery workshop."],
            ["Budget ownership is unclear.", "The buying committee may differ from the current opportunity."],
            ["Map expansion stakeholders", "Draft discovery workshop agenda", "Introduce service operations specialist"]),
        ["executive-alignment"] = new(
            "Executive alignment",
            "Executive alignment meeting",
            ["The executive sponsor wants a simpler narrative for board approval.", "The seller positioned the work around customer experience and margin protection.", "Both parties agreed success metrics should be made explicit."],
            ["Board approval depends on a stronger financial story.", "Benefits ownership needs to be assigned."],
            ["Create executive briefing", "Define success metrics", "Schedule board-prep review"]),
        ["competitor-displacement"] = new(
            "Competitor displacement",
            "Competitive positioning review",
            ["The customer is comparing the proposal with an incumbent supplier.", "The seller highlighted integration strength and lower operational friction.", "A proof point from a similar customer would help progress the decision."],
            ["Incumbent relationship may slow displacement.", "Technical evaluation criteria need tightening."],
            ["Send relevant customer proof point", "Document technical differentiators", "Arrange solution architect session"])
    };

    public IReadOnlyList<KeyValuePair<string, string>> ThemeOptions => _themes.Select(t => new KeyValuePair<string, string>(t.Key, t.Value.Label)).ToList();

    public TranscriptResult Generate(TranscriptInput input)
    {
        var theme = _themes.TryGetValue(input.Theme, out var selectedTheme) ? selectedTheme : _themes["renewal-risk"];
        var cues = BuildConversation(input, theme);
        var summary = $"The meeting focused on {theme.Label.ToLowerInvariant()} for {input.RecordName}. {input.CustomerAccountName} confirmed interest but asked for clearer evidence, ownership, and timing before committing to the next step.";
        var title = string.IsNullOrWhiteSpace(input.AppointmentSubject)
            ? $"{theme.Subject} - {input.RecordName}"
            : input.AppointmentSubject;

        return new TranscriptResult
        {
            Input = input,
            ThemeLabel = theme.Label,
            Title = title,
            Summary = summary,
            Decisions =
            [
                $"Proceed with a focused follow-up session tied to {input.RecordName}.",
                "Use a concise evidence pack to support stakeholder alignment.",
                "Track actions against named owners before the next review."
            ],
            Points = theme.Points.Select(point => PersonalizeCustomerText(point, input.CustomerAccountName)).ToList(),
            Risks = theme.Risks.Select(risk => PersonalizeCustomerText(risk, input.CustomerAccountName)).ToList(),
            Tasks = theme.Tasks.Select((task, index) => new FollowUpTask(task, index == 1 ? input.CustomerName : input.SellerName, index == 0 ? "2 business days" : index == 1 ? "1 week" : "Before next meeting")).ToList(),
            Cues = cues
        };
    }

    private static IReadOnlyList<TranscriptCue> BuildConversation(TranscriptInput input, Theme theme)
    {
        var detail = input.Length == "detailed" ? 34 : input.Length == "medium" ? 28 : 22;
        var opener = input.Tone == "tense" ? "I want to be direct about the concerns on our side." : "Thanks for making the time today.";
        var reassurance = input.Tone == "positive" ? "That gives us a strong basis to move quickly." : input.Tone == "recovery" ? "We can recover confidence if we make the next steps concrete." : "That makes sense, and we can make the next step more specific.";

        var lines = new List<(SpeakerRole Role, string Speaker, string Text)>
        {
            (SpeakerRole.Customer, input.CustomerName, opener),
            (SpeakerRole.Seller, input.SellerName, $"Absolutely. From our side, I want to understand what needs to be true for {input.RecordName} to move forward with confidence."),
            (SpeakerRole.Customer, input.CustomerName, PersonalizeCustomerText(theme.Points[0], input.CustomerAccountName) + " We are not saying no, but we need a clearer basis for the decision."),
            (SpeakerRole.Seller, input.SellerName, reassurance + " I would suggest we anchor this around outcomes, owners, and timing so the next step is easy to defend internally."),
            (SpeakerRole.Customer, input.CustomerName, PersonalizeCustomerText(string.IsNullOrWhiteSpace(input.Notes) ? theme.Points[1] : input.Notes, input.CustomerAccountName)),
            (SpeakerRole.Seller, input.SellerName, "That is helpful context. The pattern I am hearing is that the business case is there, but the proof and governance need to be sharper."),
            (SpeakerRole.Customer, input.CustomerName, PersonalizeCustomerText(theme.Risks[0], input.CustomerAccountName) + " Our leadership team will ask how we know the value is real and not just optimistic."),
            (SpeakerRole.Seller, input.SellerName, "On our end, we can bring evidence from comparable programmes and separate the benefits into quick wins, measurable adoption, and longer-term operating improvements."),
            (SpeakerRole.Customer, input.CustomerName, $"That would help, but the short-term wins need to be credible. {input.CustomerAccountName} has seen projects promise speed before and then lose momentum."),
            (SpeakerRole.Seller, input.SellerName, "Understood. I would not position this as a big-bang transformation. I would position it as a controlled first phase with visible checkpoints every two weeks."),
            (SpeakerRole.Customer, input.CustomerName, $"If we did that, who would own the adoption side? That is usually where things slow down for {input.CustomerAccountName}."),
            (SpeakerRole.Seller, input.SellerName, "We can own the enablement plan with your business lead. I would recommend naming one sponsor, one operational owner, and one technical owner before kickoff."),
            (SpeakerRole.Customer, input.CustomerName, $"That sounds sensible. I also want the {input.CustomerAccountName} client-facing teams represented because they will feel the impact first."),
            (SpeakerRole.Seller, input.SellerName, "Agreed. We can include them in the discovery workshop and use their feedback to shape the first release scope."),
            (SpeakerRole.Customer, input.CustomerName, PersonalizeCustomerText(theme.Points[1], input.CustomerAccountName)),
            (SpeakerRole.Seller, input.SellerName, "From our side, the immediate action is to turn that into a short decision pack: what changes, why it matters, who owns it, and how we measure success."),
            (SpeakerRole.Customer, input.CustomerName, "The measurement point is important. The sponsor will want two or three metrics rather than a long list."),
            (SpeakerRole.Seller, input.SellerName, "I suggest three: cycle time reduction, adoption by priority users, and reduction in manual follow-up. We can tailor those to your reporting language."),
            (SpeakerRole.Customer, input.CustomerName, "Good. We also need to be clear about what is out of scope, otherwise people will assume this solves everything at once."),
            (SpeakerRole.Seller, input.SellerName, "Completely agree. I will include explicit exclusions and a backlog section so future ideas are captured without distracting the first phase."),
            (SpeakerRole.Customer, input.CustomerName, PersonalizeCustomerText(theme.Risks[1], input.CustomerAccountName)),
            (SpeakerRole.Seller, input.SellerName, "To reduce that risk, I will propose a sponsor checkpoint before the next commercial review. That gives us space to confirm priorities before we talk numbers again."),
            (SpeakerRole.Customer, input.CustomerName, "That would make the commercial discussion easier. Procurement will still ask for options."),
            (SpeakerRole.Seller, input.SellerName, "We can provide a recommended option and a lower-risk phased option. I will avoid giving too many choices because that usually slows the decision."),
            (SpeakerRole.Customer, input.CustomerName, "Please also include assumptions. If the assumptions are visible, I can explain the dependency on our side."),
            (SpeakerRole.Seller, input.SellerName, "Yes. I will separate our assumptions from your dependencies and highlight anything that could change the timeline or cost."),
            (SpeakerRole.Customer, input.CustomerName, "What do you need from us this week?"),
            (SpeakerRole.Seller, input.SellerName, "Two things: confirmation of the sponsor attendees, and access to one operational lead who can validate the adoption priorities."),
            (SpeakerRole.Customer, input.CustomerName, "I can get you both. Send me the proposed agenda and I will forward it internally."),
            (SpeakerRole.Seller, input.SellerName, "Great. I will send the agenda, decision pack outline, and proposed success metrics by tomorrow afternoon."),
            (SpeakerRole.Customer, input.CustomerName, "That works. If the pack is clear enough, I think we can keep momentum into next week."),
            (SpeakerRole.Seller, input.SellerName, "That is the goal. I will also include a short summary of today so there is a clean record of decisions and follow-ups."),
            (SpeakerRole.Customer, input.CustomerName, "Perfect. Thanks for making this practical rather than turning it into another broad strategy conversation."),
            (SpeakerRole.Seller, input.SellerName, "Thank you. We will keep it focused, measurable, and easy for your stakeholders to act on.")
        };

        return lines.Take(detail).Select((line, index) =>
        {
            var elapsedSeconds = Math.Min(95, (int)Math.Round(index * (95.0 / Math.Max(1, detail - 1))));
            var offset = TimeSpan.FromSeconds(elapsedSeconds);
            var rawLine = $"00:{offset.Minutes:00}:{offset.Seconds:00} {line.Speaker}: {line.Text}";
            return new TranscriptCue(offset, line.Role, line.Speaker, line.Text, rawLine);
        }).ToList();
    }

    private static string PersonalizeCustomerText(string value, string customerAccountName)
    {
        return value
            .Replace("The customer", customerAccountName, StringComparison.Ordinal)
            .Replace("the customer", customerAccountName, StringComparison.Ordinal)
            .Replace("customer-facing", "client-facing", StringComparison.Ordinal)
            .Replace("customer", customerAccountName, StringComparison.Ordinal);
    }
}
