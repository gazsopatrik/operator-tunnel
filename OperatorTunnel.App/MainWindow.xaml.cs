using Microsoft.Win32;
using OperatorTunnel.Core.Backend;
using OperatorTunnel.Core.Profiles;
using OperatorTunnel.Core.Security;
using OperatorTunnel.Core.Tunnel;
using System.Security.Cryptography;
using System.IO;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;

namespace OperatorTunnel.App;

public partial class MainWindow : Window
{
    private WireGuardProfile? _activeProfile;
    private readonly TunnelStateMachine _tunnelState = new();
    private readonly IWireGuardBackend _backend = new DemoWireGuardBackend();
    private readonly EncryptedProfileStore _profileStore = new(new DpapiSecretProtector());

    public MainWindow() => InitializeComponent();

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var connected = StatusLabel.Text == "TUNNEL ONLINE";

        var transition = connected
            ? _tunnelState.BeginDisconnect()
            : _tunnelState.BeginConnect(_activeProfile is not null);

        if (!transition.Accepted)
        {
            MessageBox.Show(transition.Error, "Tunnel state blocked", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var backendResult = connected
            ? await _backend.StopAsync(_activeProfile?.Name ?? "demo")
            : await _backend.StartAsync(_activeProfile?.Name ?? "demo");

        if (!backendResult.Succeeded)
        {
            _tunnelState.Fail(backendResult.Error ?? "Backend operation failed.");
            StatusLabel.Text = "TUNNEL ERROR";
            StatusLabel.Foreground = (Brush)FindResource("Warning");
            MessageBox.Show(backendResult.Error, "Backend operation failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (connected)
        {
            _tunnelState.CompleteDisconnect();
        }
        else
        {
            _tunnelState.CompleteConnect();
        }

        StatusLabel.Text = connected ? "TUNNEL OFFLINE" : "TUNNEL ONLINE";
        StatusLabel.Foreground = connected ? (Brush)FindResource("Muted") : (Brush)FindResource("Green");
        StatusDot.Fill = connected ? new SolidColorBrush(Color.FromRgb(100, 116, 139)) : (Brush)FindResource("Green");
        ConnectButton.Content = connected ? "CONNECT" : "DISCONNECT";
        ConnectButton.Foreground = connected ? (Brush)FindResource("Neon") : (Brush)FindResource("Green");
        ConnectButton.BorderBrush = connected ? (Brush)FindResource("Neon") : (Brush)FindResource("Green");
        ProfileLabel.Text = connected ? "Demo state only — WireGuard service is not connected" : "Select a profile to begin";
    }

    private async void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "WireGuard configuration (*.conf)|*.conf|All files (*.*)|*.*", Title = "Import WireGuard profile" };
        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var fileName = System.IO.Path.GetFileName(dialog.FileName);
            var configText = System.IO.File.ReadAllText(dialog.FileName);
            var profileName = ToSafeProfileName(System.IO.Path.GetFileNameWithoutExtension(fileName));
            var parseResult = new WireGuardConfigParser().Parse(configText, profileName);

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

            try
            {
                await _profileStore.SaveAsync(profile);
            }
            catch (IOException)
            {
                _activeProfile = null;
                ProfileLabel.Text = "Import blocked: encrypted profile save failed";
                MessageBox.Show("The profile was not activated because encrypted storage failed.", "Secure save failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                _activeProfile = null;
                ProfileLabel.Text = "Import blocked: profile storage is not accessible";
                MessageBox.Show("The profile was not activated because secure storage is not accessible.", "Secure save failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch (CryptographicException)
            {
                _activeProfile = null;
                ProfileLabel.Text = "Import blocked: DPAPI protection failed";
                MessageBox.Show("The profile was not activated because Windows DPAPI protection failed.", "Secure save failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

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

    private static string ToSafeProfileName(string name)
    {
        var normalized = new string(name.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_').ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "imported-profile" : normalized[..Math.Min(normalized.Length, 64)];
    }
}
