using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace OperatorTunnel.App;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var connected = StatusLabel.Text == "TUNNEL ONLINE";
        StatusLabel.Text = connected ? "TUNNEL OFFLINE" : "TUNNEL ONLINE";
        StatusLabel.Foreground = connected ? (Brush)FindResource("Muted") : (Brush)FindResource("Green");
        StatusDot.Fill = connected ? new SolidColorBrush(Color.FromRgb(100, 116, 139)) : (Brush)FindResource("Green");
        ConnectButton.Content = connected ? "CONNECT" : "DISCONNECT";
        ConnectButton.Foreground = connected ? (Brush)FindResource("Cyan") : (Brush)FindResource("Green");
        ConnectButton.BorderBrush = connected ? (Brush)FindResource("Cyan") : (Brush)FindResource("Green");
        ProfileLabel.Text = connected ? "Demo state only — WireGuard service is not connected" : "Select a profile to begin";
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "WireGuard configuration (*.conf)|*.conf|All files (*.*)|*.*", Title = "Import WireGuard profile" };
        if (dialog.ShowDialog() == true)
            ProfileLabel.Text = $"Selected for validation: {System.IO.Path.GetFileName(dialog.FileName)}";
    }
}

