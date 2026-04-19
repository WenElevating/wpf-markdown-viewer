using System.Windows;

namespace WpfMarkdownEditor.Sample;

public partial class App : Application
{
    private void App_OnStartup(object sender, StartupEventArgs e)
    {
        var filePath = e.Args.Length > 0 ? e.Args[0] : null;
        var mainWindow = new MainWindow(filePath);
        mainWindow.Show();
    }
}
