using System.Text.Json;
using MusicPlayer.DecoderTests;

var temporary = args.Length == 0;
var report = temporary ? Path.Combine(Path.GetTempPath(), $"music-decoder-{Guid.NewGuid():N}.json") : args[0];
try
{
    AudioDiagnostics.Verify(report);
    using var document = JsonDocument.Parse(File.ReadAllText(report));
    foreach (var item in document.RootElement.EnumerateArray())
        Console.WriteLine($"PASS decode/seek/FFT: {item.GetProperty("file").GetString()}");
    Console.WriteLine("5 real WAV files decoded and verified through NAudio");
}
finally { if (temporary && File.Exists(report)) File.Delete(report); }
