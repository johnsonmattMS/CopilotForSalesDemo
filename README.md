# C4S Transcript Player

A Windows desktop demo app for generating a synthetic Copilot for Sales conversation and playing the seller and customer sides through separate audio outputs. It is designed for Teams demos where each side of a generated conversation needs to be routed into separate virtual audio channels.

The repository includes a ready-to-run Windows build, so most users do not need to install the .NET SDK or build the project.

## What It Does

- Loads an appointment context from Dataverse/D365, or uses built-in sample data.
- Generates a synthetic seller/customer transcript from the chosen scenario, tone, length, and notes.
- Shows the generated transcript and follow-up tasks.
- Plays the transcript using two selectable Windows speech voices.
- Routes seller and customer audio to separate output devices.
- Supports Voicemeeter Banana, VB-CABLE, and manual audio routing setups.
- Includes a bundled Voicemeeter Banana installer ZIP and an in-app installer launch button.
- Remembers local playback preferences, including voices, devices, speed, start delay, routing preset, and mixer path.

## Quick Start

1. Download or clone this repository.
2. Open the published app folder:

   ```text
   Release\C4STranscriptPlayer
   ```

3. Run:

   ```text
   C4STranscriptPlayer.exe
   ```

4. If Windows SmartScreen appears, choose **More info** and then **Run anyway** if you trust the source.
5. Use **Install mixer** if Voicemeeter Banana is not already installed.
6. Use **Launch mixer** before playback.
7. Choose your routing preset, voices, output devices, voice speed, and start delay.
8. Click **Generate transcript**, then **Play**.

## Installing For A Colleague

The easiest distribution method is to share the full published folder:

```text
Release\C4STranscriptPlayer
```

Send the folder as a ZIP through Teams, SharePoint, OneDrive, or another internal share. The recipient should extract the ZIP first and then run:

```text
C4STranscriptPlayer.exe
```

Do not run the app directly from inside a ZIP file. The app needs access to its supporting files and bundled installer folder.

The published build is self-contained for Windows x64, so colleagues should not need to install the .NET runtime separately.

## What Needs To Be Included

Keep these files and folders together when distributing the app:

```text
Release\C4STranscriptPlayer\C4STranscriptPlayer.exe
Release\C4STranscriptPlayer\*.dll
Release\C4STranscriptPlayer\Installers\VoicemeeterSetup_v2122.zip
Release\C4STranscriptPlayer\runtimes\
Release\C4STranscriptPlayer\cs\, de\, es\, fr\, etc.
```

The app may not start correctly if only the `.exe` is copied by itself.

## Voicemeeter Banana Setup

Voicemeeter Banana is required when you want Teams to receive the seller and customer channels separately.

1. Open the app.
2. Click **Install mixer**.
3. Complete the Voicemeeter Banana installer.
4. Restart Windows if the installer asks you to.
5. Open the app again.
6. Click **Launch mixer**.
7. Select the **Voicemeeter free two-speaker setup** routing preset.
8. Match the Teams audio inputs to the Voicemeeter channels shown by the app/help screen.

The app stores the Voicemeeter path locally. If Voicemeeter is installed somewhere unusual, update the **Voicemeeter Banana path** field in the right-hand audio panel.

## Using The App

### Dataverse

- Enter the Dataverse environment URL.
- Enter an appointment ID if you want to load a real appointment.
- Pick the Edge profile to use for sign-in.
- Click **Load appointment**.

If you do not need a live appointment, click **Use defaults** and work from the sample context.

### Scenario

Use the left-hand panel to set the demo context:

- Appointment subject
- Meeting date and duration
- D365 record
- Customer account
- Seller and customer names
- Theme
- Tone
- Length
- Scenario notes

Click **Generate transcript** after changing scenario details.

### Audio Routing

Use the right-hand panel to configure playback:

- **Routing preset**: selects a recommended audio routing pattern.
- **Playback mode**: plays both speakers, seller only, or customer only.
- **Seller output device**: where seller audio is sent.
- **Customer output device**: where customer audio is sent.
- **Seller voice** and **Customer voice**: Windows speech voices used for each side.
- **Voice speed**: controls speech rate.
- **Start delay**: waits before playback starts, useful when switching into Teams.
- **Speak short speaker-name cues**: adds short spoken cues before speaker turns.

Click **Refresh devices** after installing new audio drivers or opening Voicemeeter.

## Saved Local Settings

The app remembers local window and playback settings in:

```text
%LOCALAPPDATA%\C4STranscriptPlayer\window-settings.json
```

This includes:

- Window size and position
- Voicemeeter Banana path
- Routing preset
- Playback mode
- Seller and customer output devices
- Seller and customer voices
- Voice speed
- Start delay
- Speaker cue preference

These settings are per Windows user and are not committed to the repository.

## Building From Source

Most users can use the included `Release\C4STranscriptPlayer` build. Developers can rebuild from source with the .NET 8 SDK.

From the repository root:

```powershell
dotnet build StandalonePlayer\C4STranscriptPlayer\C4STranscriptPlayer.csproj
```

To create a fresh self-contained Windows distribution:

```powershell
dotnet publish StandalonePlayer\C4STranscriptPlayer\C4STranscriptPlayer.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o Release\C4STranscriptPlayer
```

If the build fails because `C4STranscriptPlayer.exe` is locked, close the running app and build again.

## Project Layout

```text
Release\C4STranscriptPlayer\             Ready-to-run Windows distribution
StandalonePlayer\C4STranscriptPlayer\    WPF source project
StandalonePlayer\C4STranscriptPlayer\Installers\
                                          Bundled Voicemeeter installer ZIP
```

## Notes

- The app is intended for Windows desktop use.
- The included release is built for Windows x64.
- The Voicemeeter installer is bundled for convenience; make sure your distribution use complies with the installer license for your organisation.
