using Microsoft.Win32;
using OperatorTunnel.Core.Backend;
using OperatorTunnel.Core.Diagnostics;
using OperatorTunnel.Core.Profiles;
using OperatorTunnel.Core.Security;
using OperatorTunnel.Core.Tunnel;
using System.Security.Cryptography;
using System.IO;
using System.Windows.Input;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfButton = System.Windows.Controls.Button;
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
    private readonly TrayIconController _trayIcon;
    private readonly SecurityEventLog _eventLog = new();
    private bool _allowExit;
    private bool _eventLogButtonHooked;
    private bool _profilesButtonHooked;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _trayIcon = new TrayIconController(ShowFromTray, ExitFromTray);
        _eventLog.Add(EventSeverity.Info, "app.started", "Operator Tunnel started in demo backend mode.");
        Loaded += (_, _) => HookEventLogButton();
        Loaded += (_, _) => HookProfilesButton();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var savedProfiles = await _profileStore.ListAsync();
            foreach (var savedProfile in savedProfiles)
            {
                try
                {
                    var restored = await _profileStore.LoadAsync(savedProfile);
                    _activeProfile = restored;
                    UpdateProfileCard(restored);
                    ProfileLabel.Text = $"RESTORED // {restored.Name} // encrypted profile ready";
                    return;
                }
                catch (InvalidDataException)
                {
                    // A corrupt profile must not prevent the app from opening.
                    // It is skipped and never activated.
                }
                catch (CryptographicException)
                {
                    // A profile protected for another user is not activated.
                }
                catch (IOException)
                {
                    // A corrupt profile must not prevent the app from opening.
                    // It is skipped and never activated.
                }
            }

            if (savedProfiles.Count > 0)
                ProfileLabel.Text = "Saved profiles found // none passed validation";
        }
        catch (IOException)
        {
            ProfileLabel.Text = "Profile store unavailable // import required";
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowExit)
        {
            _trayIcon.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void ShowFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void HookEventLogButton()
    {
        if (_eventLogButtonHooked)
            return;

        var eventLogButton = FindVisualChildren<WpfButton>(this)
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "[04]  EVENT LOG", StringComparison.Ordinal));
        if (eventLogButton is not null)
        {
            eventLogButton.Click += EventLogButton_Click;
            _eventLogButtonHooked = true;
        }
    }

    private void EventLogButton_Click(object sender, RoutedEventArgs e)
    {
        _eventLog.Add(EventSeverity.Info, "event_log.opened", "Event log opened.");
        var window = new EventLogWindow(_eventLog.Snapshot()) { Owner = this };
        window.ShowDialog();
    }

    private void HookProfilesButton()
    {
        if (_profilesButtonHooked)
            return;

        var profilesButton = FindVisualChildren<WpfButton>(this)
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "[02]  PROFILES", StringComparison.Ordinal));
        if (profilesButton is not null)
        {
            profilesButton.Click += ProfilesButton_Click;
            _profilesButtonHooked = true;
        }
    }

    private void ProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new ProfileManagerWindow(_profileStore, ActivateProfile) { Owner = this };
        window.ShowDialog();
    }

    private void ActivateProfile(WireGuardProfile profile)
    {
        _activeProfile = profile;
        UpdateProfileCard(profile);
        ProfileLabel.Text = $"LOADED // {profile.Name} // encrypted profile ready";
        _eventLog.Add(EventSeverity.Info, "profile.loaded", $"Profile {profile.Name} loaded from encrypted storage.");
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ExitFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            _allowExit = true;
            Close();
        });
    }

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
            _eventLog.Add(EventSeverity.Error, "tunnel.backend_failed", "Backend operation failed.");
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
        _eventLog.Add(EventSeverity.Info, connected ? "tunnel.disconnected" : "tunnel.connected", connected ? "Demo tunnel disconnected." : "Demo tunnel connected.");
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
                _eventLog.Add(EventSeverity.Warning, "profile.validation_failed", $"Profile validation failed for {fileName}.");
                ShowValidationIssues(parseResult.Issues.Select(issue => $"line {issue.Line}: {issue.Message}"));
                return;
            }

            var validation = new WireGuardProfileValidator().Validate(parseResult.Profile!);
            if (!validation.IsValid)
            {
                _activeProfile = null;
                ProfileLabel.Text = $"Validation failed: {fileName}";
                ResetProfileCard();
                _eventLog.Add(EventSeverity.Warning, "profile.semantic_validation_failed", $"Profile semantic validation failed for {fileName}.");
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
                _eventLog.Add(EventSeverity.Error, "profile.secure_save_failed", "Encrypted profile save failed.");
                MessageBox.Show("The profile was not activated because encrypted storage failed.", "Secure save failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                _activeProfile = null;
                ProfileLabel.Text = "Import blocked: profile storage is not accessible";
                _eventLog.Add(EventSeverity.Error, "profile.storage_unavailable", "Encrypted profile storage was not accessible.");
                MessageBox.Show("The profile was not activated because secure storage is not accessible.", "Secure save failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            catch (CryptographicException)
            {
                _activeProfile = null;
                ProfileLabel.Text = "Import blocked: DPAPI protection failed";
                _eventLog.Add(EventSeverity.Error, "profile.dpapi_failed", "Windows DPAPI protection failed.");
                MessageBox.Show("The profile was not activated because Windows DPAPI protection failed.", "Secure save failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _activeProfile = profile;
            var endpoint = profile.Peers.FirstOrDefault()?.Endpoint;
            var allowedIps = profile.Peers.SelectMany(peer => peer.AllowedIps).Distinct(StringComparer.OrdinalIgnoreCase);
            ProfileLabel.Text = $"VALIDATED // {fileName} // {profile.InterfaceAddress} // {endpoint ?? "no endpoint"} // {string.Join(", ", allowedIps)}";
            UpdateProfileCard(profile);
            _eventLog.Add(EventSeverity.Info, "profile.imported", $"Profile {profile.Name} imported and saved securely.");
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
