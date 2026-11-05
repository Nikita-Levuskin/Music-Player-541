using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace MusicPlayer;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        if (e.Args.Length == 2 && e.Args[0] == "--screenshot")
        {
            window.ContentRendered += (_, _) => window.Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle, () =>
                {
                    try
                    {
                        var bitmap = new RenderTargetBitmap((int)window.ActualWidth,
                            (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var stream = File.Create(e.Args[1]);
                        encoder.Save(stream);
                        Shutdown(0);
                    }
                    catch { Shutdown(1); }
                });
        }
        window.Show();
    }
}
