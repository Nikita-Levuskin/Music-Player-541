using System.IO;
using System.Text.Json;
using MusicPlayer.Core;
using NAudio.Wave;

namespace MusicPlayer.DecoderTests;

internal static class AudioDiagnostics
{
    public static void Verify(string reportPath)
    {
        var tracks = PlaylistService.Scan(Path.Combine(AppContext.BaseDirectory, "audio"));
        if (tracks.Count != 5) throw new InvalidOperationException("Expected five demo tracks");
        var results = new List<object>();
        foreach (var track in tracks)
        {
            using var reader = new AudioFileReader(track.Path);
            if (reader.TotalTime.TotalSeconds < 17) throw new InvalidDataException("Demo is too short");
            var analyzer = new SpectrumAnalyzer();
            var buffer = new float[4096];
            float peak = 0; double spectrumPeak = 0; long samples = 0;
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                samples += read;
                for (int i = 0; i < read; i++) peak = Math.Max(peak, Math.Abs(buffer[i]));
                analyzer.Push(buffer, 0, read, reader.WaveFormat.Channels);
                spectrumPeak = Math.Max(spectrumPeak, analyzer.Snapshot().Max());
            }
            if (samples == 0 || peak < 0.01 || spectrumPeak <= 0)
                throw new InvalidDataException("No decoded signal or spectrum");
            reader.CurrentTime = TimeSpan.FromSeconds(9);
            if (reader.Read(buffer, 0, buffer.Length) == 0)
                throw new InvalidDataException("Seek did not return samples");
            results.Add(new { file = Path.GetFileName(track.Path), durationSeconds = reader.TotalTime.TotalSeconds,
                channels = reader.WaveFormat.Channels, sampleRate = reader.WaveFormat.SampleRate,
                decodedSamples = samples, pcmPeak = peak, spectrumPeak, seekVerified = true });
        }
        File.WriteAllText(reportPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }
}
