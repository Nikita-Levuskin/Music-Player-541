using System.ComponentModel;

namespace MusicPlayer.Core;

public sealed class Track(string path) : INotifyPropertyChanged
{
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public string Title { get; } = System.IO.Path.GetFileNameWithoutExtension(path);
    public string? Error { get; private set; }
    public string DisplayName => Error is null ? Title : $"{Title} (недоступен)";
    public event PropertyChangedEventHandler? PropertyChanged;
    public void SetError(string? error)
    {
        Error = error;
        PropertyChanged?.Invoke(this, new(nameof(Error)));
        PropertyChanged?.Invoke(this, new(nameof(DisplayName)));
    }
}

public static class PlaylistService
{
    public static IReadOnlyList<Track> Scan(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory).Where(p =>
                new[] { ".mp3", ".wav" }.Contains(System.IO.Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .OrderBy(System.IO.Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(p => new Track(p)).ToArray();
    }
}

public static class PlaybackMath
{
    public static double ClampVolume(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
    public static TimeSpan ClampPosition(double seconds, TimeSpan duration) =>
        TimeSpan.FromSeconds(double.IsFinite(seconds) ? Math.Clamp(seconds, 0, Math.Max(0, duration.TotalSeconds)) : 0);
    public static string FormatTime(TimeSpan value) => value.TotalHours >= 1
        ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
        : $"{Math.Max(0, (int)value.TotalMinutes)}:{Math.Max(0, value.Seconds):00}";
}
