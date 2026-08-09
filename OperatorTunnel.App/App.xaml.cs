using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;

namespace OperatorTunnel.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: "Local\\OperatorTunnel.SingleInstance",
            createdNew: out var createdNew);

        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "Operator Tunnel is already running. Check the system tray.",
                "Operator Tunnel",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}

