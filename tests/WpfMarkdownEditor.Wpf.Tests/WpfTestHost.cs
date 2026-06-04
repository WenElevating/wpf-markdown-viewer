using System.Threading;
using System.Windows;
using System.Windows.Threading;
using WpfMarkdownEditor.Sample;

namespace WpfMarkdownEditor.Wpf.Tests;

internal static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> TestDispatcher = new(StartDispatcher);

    public static void Run(Action action)
    {
        TestDispatcher.Value.Invoke(action);
    }

    private static Dispatcher StartDispatcher()
    {
        Dispatcher? dispatcher = null;
        Exception? startupException = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            try
            {
                dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(dispatcher));

                if (Application.Current is null)
                {
                    var app = new App();
                    app.InitializeComponent();
                }
            }
            catch (Exception ex)
            {
                startupException = ex;
            }
            finally
            {
                ready.Set();
            }

            if (startupException is null)
                Dispatcher.Run();
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();

        if (startupException is not null)
            throw startupException;

        return dispatcher ?? throw new InvalidOperationException("WPF test dispatcher was not initialized.");
    }
}
