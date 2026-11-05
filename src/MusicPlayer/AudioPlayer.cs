using MusicPlayer.Core;
using NAudio.Wave;

namespace MusicPlayer;

public sealed class AudioPlayer : IDisposable
{
    private WaveOutEvent? output;
    private AudioFileReader? reader;
    private float volume = 0.7f;
    public SpectrumAnalyzer Analyzer { get; } = new();
    public event Action? Ended;
    public event Action<string>? Failed;
    public PlaybackState State => output?.PlaybackState ?? PlaybackState.Stopped;
    public bool IsLoaded => reader is not null;
    public TimeSpan Position => reader?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => reader?.TotalTime ?? TimeSpan.Zero;
    public double Volume
    {
        get => volume;
        set { volume = (float)PlaybackMath.ClampVolume(value); if (output is not null) output.Volume = volume; }
    }

    public void Load(string path)
    {
        CloseTrack();
        AudioFileReader? nextReader = null;
        WaveOutEvent? nextOutput = null;
        try
        {
            nextReader = new AudioFileReader(path);
            nextOutput = new WaveOutEvent { DesiredLatency = 120, Volume = volume };
            nextOutput.Init(new VisualizationProvider(nextReader, Analyzer));
            reader = nextReader; output = nextOutput;
            output.PlaybackStopped += OnStopped;
        }
        catch
        {
            nextOutput?.Dispose(); nextReader?.Dispose(); throw;
        }
    }
    public void Play() { if (reader is null) throw new InvalidOperationException("Сначала выберите трек"); output!.Play(); }
    public void Pause() => output?.Pause();
    public void Stop()
    {
        if (output is null || reader is null) return;
        output.Stop(); reader.CurrentTime = TimeSpan.Zero; Analyzer.Clear();
    }
    public void Seek(double seconds)
    {
        if (reader is null) return;
        reader.CurrentTime = PlaybackMath.ClampPosition(seconds, reader.TotalTime); Analyzer.Clear();
    }
    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null) Failed?.Invoke(e.Exception.Message);
        else if (reader is not null && reader.CurrentTime >= reader.TotalTime) Ended?.Invoke();
    }
    private void CloseTrack()
    {
        if (output is not null)
        {
            output.PlaybackStopped -= OnStopped;
            output.Stop(); output.Dispose(); output = null;
        }
        reader?.Dispose(); reader = null; Analyzer.Clear();
    }
    public void Dispose() => CloseTrack();
    public void Unload() => CloseTrack();

    private sealed class VisualizationProvider(ISampleProvider source, SpectrumAnalyzer analyzer) : ISampleProvider
    {
        public WaveFormat WaveFormat => source.WaveFormat;
        public int Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);
            analyzer.Push(buffer, offset, read, WaveFormat.Channels);
            return read;
        }
    }
}
