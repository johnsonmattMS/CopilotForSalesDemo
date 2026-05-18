using System.IO;
using System.Speech.Synthesis;
using NAudio.Wave;

namespace C4STranscriptPlayer;

public sealed class AudioPlaybackService
{
    private CancellationTokenSource? _playbackCancellation;

    public IReadOnlyList<AudioOutputDevice> GetOutputDevices()
    {
        return Enumerable.Range(-1, WaveOut.DeviceCount + 1)
            .Select(deviceNumber =>
            {
                if (deviceNumber == -1) return new AudioOutputDevice(-1, "System default output");
                var capabilities = WaveOut.GetCapabilities(deviceNumber);
                return new AudioOutputDevice(deviceNumber, capabilities.ProductName);
            })
            .ToList();
    }

    public IReadOnlyList<SpeechVoice> GetVoices()
    {
        using var synth = new SpeechSynthesizer();
        return synth.GetInstalledVoices()
            .Where(voice => voice.Enabled)
            .Select(voice => new SpeechVoice(voice.VoiceInfo.Name))
            .OrderBy(voice => voice.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Stop()
    {
        _playbackCancellation?.Cancel();
    }

    public async Task PlayAsync(TranscriptResult transcript, PlaybackOptions options, IProgress<string> progress, CancellationToken cancellationToken = default)
    {
        Stop();
        _playbackCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _playbackCancellation.Token;

        var cues = transcript.Cues.Where(cue => options.Mode switch
        {
            PlaybackMode.SellerOnly => cue.Role == SpeakerRole.Seller,
            PlaybackMode.CustomerOnly => cue.Role == SpeakerRole.Customer,
            _ => true
        }).ToList();

        if (cues.Count == 0)
        {
            progress.Report("No transcript lines match this playback mode.");
            return;
        }

        progress.Report($"Starting in {options.StartDelaySeconds} seconds...");
        await Task.Delay(TimeSpan.FromSeconds(options.StartDelaySeconds), token);

        var firstOffset = cues[0].Offset;
        var previousOffset = firstOffset;
        for (var index = 0; index < cues.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            var cue = cues[index];
            var gap = index == 0 ? cue.Offset - firstOffset : cue.Offset - previousOffset;
            gap = ShortenPause(gap);
            if (gap > TimeSpan.Zero) await Task.Delay(gap, token);

            var device = cue.Role == SpeakerRole.Seller ? options.SellerDevice : options.CustomerDevice;
            var voice = cue.Role == SpeakerRole.Seller ? options.SellerVoice : options.CustomerVoice;
            var text = options.SpeakSpeakerCues ? $"{cue.Speaker} says. {cue.Text}" : cue.Text;
            progress.Report($"Playing {index + 1} of {cues.Count}: {cue.Speaker} -> {device?.Name ?? "System default output"}");

            var wavPath = SynthesizeToTempWav(text, voice, cue.Role, options.VoiceRate);
            try
            {
                await PlayWavAsync(wavPath, device?.DeviceNumber ?? -1, token);
            }
            finally
            {
                TryDelete(wavPath);
            }

            previousOffset = cue.Offset;
        }

        progress.Report("Playback complete.");
    }

    private static string SynthesizeToTempWav(string text, string? voiceName, SpeakerRole role, int voiceRate)
    {
        var path = Path.Combine(Path.GetTempPath(), $"c4s-transcript-{Guid.NewGuid():N}.wav");
        using var synth = new SpeechSynthesizer();
        if (!string.IsNullOrWhiteSpace(voiceName)) synth.SelectVoice(voiceName!);
        synth.Rate = Math.Clamp(voiceRate, -10, 10);
        synth.Volume = role == SpeakerRole.Seller ? 96 : 100;
        synth.SetOutputToWaveFile(path);
        synth.Speak(text);
        synth.SetOutputToNull();
        return path;
    }

    private static Task PlayWavAsync(string path, int deviceNumber, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        WaveOutEvent? output = null;
        AudioFileReader? reader = null;

        try
        {
            reader = new AudioFileReader(path);
            output = new WaveOutEvent { DeviceNumber = deviceNumber };
            output.Init(reader);
            output.PlaybackStopped += (_, args) =>
            {
                reader.Dispose();
                output.Dispose();
                if (args.Exception != null) completion.TrySetException(args.Exception);
                else completion.TrySetResult();
            };
            var registration = cancellationToken.Register(() =>
            {
                try { output.Stop(); }
                catch { completion.TrySetCanceled(cancellationToken); }
            });
            completion.Task.ContinueWith(_ => registration.Dispose(), TaskScheduler.Default);
            output.Play();
            return completion.Task;
        }
        catch
        {
            reader?.Dispose();
            output?.Dispose();
            throw;
        }
    }

    private static TimeSpan ShortenPause(TimeSpan originalGap)
    {
        if (originalGap <= TimeSpan.Zero) return TimeSpan.Zero;

        var shortened = TimeSpan.FromMilliseconds(originalGap.TotalMilliseconds * 0.12);
        if (shortened < TimeSpan.FromMilliseconds(250)) return TimeSpan.FromMilliseconds(250);
        if (shortened > TimeSpan.FromMilliseconds(900)) return TimeSpan.FromMilliseconds(900);
        return shortened;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { }
    }
}
