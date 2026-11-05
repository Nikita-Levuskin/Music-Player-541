using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MusicPlayer.Core;
using NAudio.Wave;

namespace MusicPlayer;

public partial class MainWindow : Window
{
    private readonly AudioPlayer player = new();
    private readonly ObservableCollection<Track> tracks = [];
    private readonly List<Rectangle> bars = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly double[] displayed = new double[SpectrumAnalyzer.BandCount];
    private Track? loaded;
    private bool dragging, updating;
    private bool closing;

    public MainWindow()
    {
        InitializeComponent();
        TrackList.ItemsSource = tracks;
        for (int i = 0; i < SpectrumAnalyzer.BandCount; i++)
        {
            Spectrum.ColumnDefinitions.Add(new ColumnDefinition());
            var bar = new Rectangle { Fill = new SolidColorBrush(Color.FromRgb(87, 193, 187)),
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(2, 0, 2, 0),
                Height = 3, RadiusX = 2, RadiusY = 2 };
            Grid.SetColumn(bar, i); Spectrum.Children.Add(bar); bars.Add(bar);
        }
        player.Ended += () => Dispatcher.BeginInvoke(() => { if (!closing) Advance(1, true); });
        player.Failed += message => Dispatcher.BeginInvoke(() => { if (!closing) Status.Text = "Ошибка аудиоустройства: " + message; });
        timer.Tick += (_, _) => Tick(); timer.Start();
        Closed += (_, _) => { closing = true; timer.Stop(); player.Dispose(); };
        LoadFolder(System.IO.Path.Combine(AppContext.BaseDirectory, "audio"));
    }

    private void LoadFolder(string directory)
    {
        try
        {
            player.Stop(); loaded = null; tracks.Clear();
            foreach (var track in PlaylistService.Scan(directory)) tracks.Add(track);
            TrackCount.Text = $"Треков: {tracks.Count}";
            Status.Text = tracks.Count == 0 ? "Папка пуста или недоступна. Откройте каталог с MP3/WAV." : directory;
            if (tracks.Count > 0) TrackList.SelectedIndex = 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { Status.Text = "Не удалось открыть папку: " + e.Message; }
    }

    private bool LoadSelected(bool play)
    {
        if (TrackList.SelectedItem is not Track track) { Status.Text = "Выберите трек в плейлисте"; return false; }
        try
        {
            if (loaded != track || !player.IsLoaded)
            {
                player.Load(track.Path); loaded = track; track.SetError(null);
                TrackTitle.Text = track.Title;
                updating = true; PositionSlider.Maximum = Math.Max(1, player.Duration.TotalSeconds);
                PositionSlider.Value = 0; updating = false;
                TotalTime.Text = PlaybackMath.FormatTime(player.Duration);
            }
            if (play) player.Play();
            Status.Text = play ? "Воспроизведение: " + track.Title : "Готово к воспроизведению";
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException or NAudio.MmException)
        { loaded = null; track.SetError(e.Message); Status.Text = "Не удалось воспроизвести: " + e.Message; return false; }
    }
    private void Tick()
    {
        if (!dragging)
        {
            updating = true; PositionSlider.Value = player.Position.TotalSeconds; updating = false;
            CurrentTime.Text = PlaybackMath.FormatTime(player.Position);
        }
        PlayButton.Content = player.State == PlaybackState.Playing ? "Пауза" : "Воспроизвести";
        var levels = player.State == PlaybackState.Playing ? player.Analyzer.Snapshot() : new double[displayed.Length];
        double height = Math.Max(20, Spectrum.ActualHeight - 4);
        for (int i = 0; i < bars.Count; i++)
        {
            displayed[i] = levels[i] > displayed[i] ? levels[i] : displayed[i] * 0.8;
            bars[i].Height = Math.Max(3, displayed[i] * height);
        }
    }
    private void PlayPause(object sender, RoutedEventArgs e)
    {
        if (player.State == PlaybackState.Playing) { player.Pause(); Status.Text = "Пауза"; }
        else LoadSelected(true);
    }
    private void Stop(object sender, RoutedEventArgs e) { player.Stop(); Status.Text = "Остановлено"; }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (IsLoaded && TrackList.SelectedItem is Track) LoadSelected(player.State == PlaybackState.Playing); }
    private void DoubleClick(object sender, MouseButtonEventArgs e) => LoadSelected(true);
    private void Previous(object sender, RoutedEventArgs e) => Advance(-1, player.State == PlaybackState.Playing);
    private void Next(object sender, RoutedEventArgs e) => Advance(1, player.State == PlaybackState.Playing);
    private void Advance(int direction, bool play)
    {
        if (tracks.Count == 0) return;
        for (int attempt = 0; attempt < tracks.Count; attempt++)
        {
            int index = (TrackList.SelectedIndex + direction + tracks.Count) % tracks.Count;
            TrackList.SelectionChanged -= SelectionChanged;
            TrackList.SelectedIndex = index;
            TrackList.SelectionChanged += SelectionChanged;
            if (LoadSelected(play)) return;
        }
        Status.Text = "Нет доступных для воспроизведения треков";
    }
    private void BeginSeek(object sender, MouseButtonEventArgs e) => dragging = true;
    private void EndSeek(object sender, MouseButtonEventArgs e)
    { if (dragging) { player.Seek(PositionSlider.Value); dragging = false; } }
    private void SeekChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    { if (!updating && !dragging && player.IsLoaded) player.Seek(e.NewValue); }
    private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        player.Volume = e.NewValue;
        if (VolumeLabel is not null) VolumeLabel.Text = $"{(int)(e.NewValue * 100)} %";
    }
    private void ChooseFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Выберите папку с MP3 или WAV" };
        if (dialog.ShowDialog(this) == true) LoadFolder(dialog.FolderName);
    }
}
