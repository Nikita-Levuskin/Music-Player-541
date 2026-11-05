using MusicPlayer.Core;

int count = 0;
void Check(string name, Action test)
{
    test(); count++; Console.WriteLine($"PASS {name}");
}
void Assert(bool condition) { if (!condition) throw new InvalidOperationException("Assertion failed"); }
Check("volume boundaries", () => { Assert(PlaybackMath.ClampVolume(-1) == 0); Assert(PlaybackMath.ClampVolume(2) == 1); });
Check("non-finite volume", () => { Assert(PlaybackMath.ClampVolume(double.NaN) == 0); Assert(PlaybackMath.ClampVolume(double.PositiveInfinity) == 0); });
Check("seek boundaries", () => { Assert(PlaybackMath.ClampPosition(-10, TimeSpan.FromSeconds(60)) == TimeSpan.Zero); Assert(PlaybackMath.ClampPosition(90, TimeSpan.FromSeconds(60)).TotalSeconds == 60); });
Check("non-finite seek", () => Assert(PlaybackMath.ClampPosition(double.NaN, TimeSpan.FromSeconds(10)) == TimeSpan.Zero));
Check("time formatting", () => { Assert(PlaybackMath.FormatTime(TimeSpan.FromSeconds(65)) == "1:05"); Assert(PlaybackMath.FormatTime(TimeSpan.FromSeconds(3661)) == "1:01:01"); });
Check("missing audio directory", () => Assert(PlaylistService.Scan(System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString())).Count == 0));
var temporary = Directory.CreateTempSubdirectory("music-player-tests-");
try
{
    File.WriteAllBytes(System.IO.Path.Combine(temporary.FullName, "b.MP3"), []);
    File.WriteAllBytes(System.IO.Path.Combine(temporary.FullName, "a.wav"), []);
    File.WriteAllText(System.IO.Path.Combine(temporary.FullName, "ignore.txt"), "not audio");
    Check("playlist formats and stable order", () => { var p = PlaylistService.Scan(temporary.FullName); Assert(p.Count == 2); Assert(p[0].Title == "a"); Assert(p[1].Title == "b"); });
    Check("corrupt track is markable without losing playlist", () => { var t = new Track(System.IO.Path.Combine(temporary.FullName, "a.wav")); t.SetError("bad file"); Assert(t.DisplayName.Contains("недоступен")); t.SetError(null); Assert(t.DisplayName == "a"); });
}
finally { temporary.Delete(true); }
Check("empty spectrum", () => Assert(new SpectrumAnalyzer().Snapshot().All(x => x == 0)));
Check("PCM silence", () => { var a = new SpectrumAnalyzer(); a.Push(new float[4096], 0, 4096, 2); Assert(a.Snapshot().All(x => x == 0)); });
Check("spectrum follows actual frequency", () =>
{
    int Peak(double f)
    {
        var a = new SpectrumAnalyzer();
        var s = Enumerable.Range(0, 4096).Select(i => (float)(0.7 * Math.Sin(2 * Math.PI * f * i / 22050))).ToArray();
        a.Push(s, 0, s.Length, 1); var b = a.Snapshot();
        Assert(b.All(x => x >= 0 && x <= 1)); Assert(b.Max() > 0.5);
        return Array.IndexOf(b, b.Max());
    }
    Assert(Peak(300) < Peak(3000));
});
Check("clear after seek", () => { var a = new SpectrumAnalyzer(); a.Push(Enumerable.Repeat(0.7f, 2048).ToArray(), 0, 2048, 1); a.Clear(); Assert(a.Snapshot().All(x => x == 0)); });
Check("invalid PCM arguments", () => { try { new SpectrumAnalyzer().Push([], 0, 0, 0); throw new Exception("Expected exception"); } catch (ArgumentOutOfRangeException) { } });
Check("non-finite PCM does not poison FFT", () => { var a = new SpectrumAnalyzer(); a.Push([float.NaN, float.PositiveInfinity], 0, 2, 1); Assert(a.Snapshot().All(double.IsFinite)); });
Console.WriteLine($"{count} tests passed");
