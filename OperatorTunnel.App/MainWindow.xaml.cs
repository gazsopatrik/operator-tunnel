using Microsoft.Win32;
using OperatorTunnel.Core.Profiles;
using System.Windows;
using System.Windows.Media;

namespace OperatorTunnel.App;

public partial class MainWindow : Window
{
    private WireGuardProfile? _activeProfile;

    public MainWindow() => InitializeComponent();

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var connected = StatusLabel.Text == "TUNNEL ONLINE";
        StatusLabel.Text = connected ? "TUNNEL OFFLINE" : "TUNNEL ONLINE";
        StatusLabel.Foreground = connected ? (Brush)FindResource("Muted") : (Brush)FindResource("Green");
        StatusDot.Fill = connected ? new SolidColorBrush(Color.FromRgb(100, 116, 139)) : (Brush)FindResource("Green");
        ConnectButton.Content = connected ? "CONNECT" : "DISCONNECT";
        ConnectButton.Foreground = connected ? (Brush)FindResource("Neon") : (Brush)FindResource("Green");
        ConnectButton.BorderBrush = connected ? (Brush)FindResource("Neon") : (Brush)FindResource("Green");
        ProfileLabel.Text = connected ? "Demo state only — WireGuard service is not connected" : "Select a profile to begin";
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "WireGuard configuration (*.conf)|*.conf|All files (*.*)|*.*", Title = "Import WireGuard profile" };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var fileName = System.IO.Path.GetFileName(dialog.FileName);
            var configText = System.IO.File.ReadAllText(dialog.FileName);
            var parseResult = new WireGuardConfigParser().Parse(configText, System.IO.Path.GetFileNameWithoutExtension(fileName));

            if (!parseResult.IsValid)
            {
                _activeProfile = null;
                ProfileLabel.Text = $"Validation failed: {fileName}";
                ShowValidationIssues(parseResult.Issues.Select(issue => $"line {issue.Line}: {issue.Message}"));
                return;
            }

            var validation = new WireGuardProfileValidator().Validate(parseResult.Profile!);
            if (!validation.IsValid)
            {
                _activeProfile = null;
                ProfileLabel.Text = $"Validation failed: {fileName}";
                ShowValidationIssues(validation.Issues.Select(issue => issue.Message));
                return;
            }

            var profile = parseResult.Profile!;
            _activeProfile = profile;
            ProfileLabel.Text = $"Validated: {fileName} // {profile.Peers.Count} peer(s) // ready for backend";
        }
        catch (System.IO.IOException)
        {
            ProfileLabel.Text = "Import failed: file could not be read";
        }
    }

    private void ShowValidationIssues(IEnumerable<string> issues)
    {
        var message = string.Join(Environment.NewLine, issues.Take(8));
        MessageBox.Show(message, "Profile validation failed", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
