using Microsoft.Win32;
using OperatorTunnel.Core.Profiles;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace OperatorTunnel.App;

public partial class MainWindow : Window
{
    private WireGuardProfile? _activeProfile;

    public MainWindow() => InitializeComponent();

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeProfile is null)
        {
            MessageBox.Show("Import and validate a WireGuard profile before connecting.", "Profile required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

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
                ResetProfileCard();
                ShowValidationIssues(parseResult.Issues.Select(issue => $"line {issue.Line}: {issue.Message}"));
                return;
            }

            var validation = new WireGuardProfileValidator().Validate(parseResult.Profile!);
            if (!validation.IsValid)
            {
                _activeProfile = null;
                ProfileLabel.Text = $"Validation failed: {fileName}";
                ResetProfileCard();
                ShowValidationIssues(validation.Issues.Select(issue => issue.Message));
                return;
            }

            var profile = parseResult.Profile!;
            _activeProfile = profile;
            var endpoint = profile.Peers.FirstOrDefault()?.Endpoint;
            var allowedIps = profile.Peers.SelectMany(peer => peer.AllowedIps).Distinct(StringComparer.OrdinalIgnoreCase);
            ProfileLabel.Text = $"VALIDATED // {fileName} // {profile.InterfaceAddress} // {endpoint ?? "no endpoint"} // {string.Join(", ", allowedIps)}";
            UpdateProfileCard(profile);
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

    private void UpdateProfileCard(WireGuardProfile profile)
    {
        var textBlocks = FindVisualChildren<TextBlock>(this).ToList();
        var profileName = textBlocks.FirstOrDefault(block => block.Text == "No profile loaded");
        if (profileName is not null)
            profileName.Text = profile.Name;

        var badge = textBlocks.FirstOrDefault(block => block.Text.StartsWith("PROFILE /", StringComparison.Ordinal));
        if (badge is not null)
            badge.Text = $"PROFILE / {profile.Peers.Count:00}";

        var placeholders = textBlocks
            .Where(block => block.Text is "--" or "—" or "â€”")
            .Take(2)
            .ToList();
        if (placeholders.Count > 0)
            placeholders[0].Text = profile.InterfaceAddress;
        if (placeholders.Count > 1)
            placeholders[1].Text = profile.Peers.FirstOrDefault()?.Endpoint ?? "no endpoint";

        var allowedIps = textBlocks.FirstOrDefault(block => block.Text == "No routing rules loaded");
        if (allowedIps is not null)
            allowedIps.Text = string.Join(", ", profile.Peers.SelectMany(peer => peer.AllowedIps).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void ResetProfileCard()
    {
        var textBlocks = FindVisualChildren<TextBlock>(this).ToList();
        var profileName = textBlocks.FirstOrDefault(block => block.Text != "No profile loaded" && block.Text.StartsWith("demo", StringComparison.OrdinalIgnoreCase));
        if (profileName is not null)
            profileName.Text = "No profile loaded";

        var badge = textBlocks.FirstOrDefault(block => block.Text.StartsWith("PROFILE /", StringComparison.Ordinal));
        if (badge is not null)
            badge.Text = "PROFILE / --";
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is null)
            yield break;

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }
}
