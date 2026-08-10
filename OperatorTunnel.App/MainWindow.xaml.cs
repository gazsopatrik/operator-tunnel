using Microsoft.Win32;
using OperatorTunnel.Audit;
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
using System.Windows.Threading;

namespace OperatorTunnel.App;

public partial class MainWindow : Window
{
    private WireGuardProfile? _activeProfile;
    private readonly TunnelStateMachine _tunnelState = new();
    private readonly IWireGuardBackend _backend = new DemoWireGuardBackend();
    private readonly EncryptedProfileStore _profileStore = new(new DpapiSecretProtector());
    private readonly IAuditProjectStore _auditProjectStore;
    private readonly TrayIconController _trayIcon;
    private readonly SecurityEventLog _eventLog = new();
    private readonly DispatcherTimer _telemetryTimer;
    private bool _telemetryRefreshInFlight;
    private bool _allowExit;
    private bool _eventLogButtonHooked;
    private bool _profilesButtonHooked;
    private bool _auditProjectsButtonHooked;
    private string? _displayedProfileName;
    private string? _displayedProfileBadge;
    private string? _displayedInterfaceAddress;
    private string? _displayedEndpoint;
    private string? _displayedAllowedIps;
    private string? _displayedHandshake;
    private string? _displayedTransfer;
    private string? _displayedHandshakeDetail;
    private string? _displayedTransferDetail;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        _trayIcon = new TrayIconController(ShowFromTray, ExitFromTray);
        var auditStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OperatorTunnel",
            "audit-projects.json");
        _auditProjectStore = new JsonAuditProjectStore(auditStorePath);
        _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _telemetryTimer.Tick += TelemetryTimer_Tick;
        _eventLog.Add(EventSeverity.Info, "app.started", "Operator Tunnel started in demo backend mode.");
        Loaded += (_, _) => HookEventLogButton();
        Loaded += (_, _) => HookProfilesButton();
        Loaded += (_, _) => HookAuditProjectsButton();
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
            _telemetryTimer.Stop();
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
        var window = new ProfileManagerWindow(_profileStore, ActivateProfile, HandleProfileDeleted, CanDeleteProfile) { Owner = this };
        window.ShowDialog();
    }

    private void HookAuditProjectsButton()
    {
        if (_auditProjectsButtonHooked)
            return;

        var auditProjectsButton = FindVisualChildren<WpfButton>(this)
            .FirstOrDefault(button => string.Equals(button.Content?.ToString(), "[05]  AUDIT PROJECTS", StringComparison.Ordinal));
        if (auditProjectsButton is not null)
        {
            auditProjectsButton.Click += AuditProjectsButton_Click;
            _auditProjectsButtonHooked = true;
        }
    }

    private void AuditProjectsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new AuditProjectManagerWindow(_auditProjectStore) { Owner = this };
        window.ShowDialog();
    }

    private void ActivateProfile(WireGuardProfile profile)
    {
        _activeProfile = profile;
        UpdateProfileCard(profile);
        ProfileLabel.Text = $"LOADED // {profile.Name} // encrypted profile ready";
        _eventLog.Add(EventSeverity.Info, "profile.loaded", $"Profile {profile.Name} loaded from encrypted storage.");
    }

    private void HandleProfileDeleted(string profileName)
    {
        if (string.Equals(_activeProfile?.Name, profileName, StringComparison.OrdinalIgnoreCase))
        {
            _activeProfile = null;
            ResetProfileCard();
            ProfileLabel.Text = "PROFILE DELETED // import or load another profile";
        }

        _eventLog.Add(EventSeverity.Info, "profile.deleted", $"Profile {profileName} deleted from encrypted storage.");
    }

    private bool CanDeleteProfile(string profileName)
    {
        if (_tunnelState.State == TunnelState.Connected &&
            string.Equals(_activeProfile?.Name, profileName, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("Disconnect the active tunnel before deleting its profile.", "Profile deletion blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
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

        if (_backend is not DemoWireGuardBackend)
        {
            var expectedStatus = connected
                ? WireGuardServiceStatus.Stopped
                : WireGuardServiceStatus.Running;
            var statusCheck = await WaitForBackendStatusAsync(_activeProfile?.Name ?? "demo", expectedStatus);
            if (!statusCheck.Succeeded)
            {
                _tunnelState.Fail(statusCheck.Error ?? "WireGuard service status verification failed.");
                StatusLabel.Text = "TUNNEL ERROR";
                StatusLabel.Foreground = (Brush)FindResource("Warning");
                _eventLog.Add(EventSeverity.Error, "tunnel.status_verification_failed", "WireGuard service status could not be verified.");
                MessageBox.Show(statusCheck.Error, "Tunnel status verification failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
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

        if (connected)
        {
            _telemetryTimer.Stop();
            ResetTelemetry();
        }
        else if (_backend is not DemoWireGuardBackend)
        {
            await RefreshTelemetryAsync(_activeProfile?.Name ?? "demo");
            _telemetryTimer.Start();
        }
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
        var endpoint = profile.Peers.FirstOrDefault()?.Endpoint ?? "no endpoint";
        var allowedIpsValue = string.Join(", ", profile.Peers.SelectMany(peer => peer.AllowedIps).Distinct(StringComparer.OrdinalIgnoreCase));
        _displayedProfileName = profile.Name;
        _displayedProfileBadge = $"PROFILE / {profile.Peers.Count:00}";
        _displayedInterfaceAddress = profile.InterfaceAddress;
        _displayedEndpoint = endpoint;
        _displayedAllowedIps = allowedIpsValue;

        var profileName = textBlocks.FirstOrDefault(block => block.Text == "No profile loaded");
        if (profileName is not null)
            profileName.Text = profile.Name;

        var badge = textBlocks.FirstOrDefault(block => block.Text.StartsWith("PROFILE /", StringComparison.Ordinal));
        if (badge is not null)
            badge.Text = _displayedProfileBadge;

        var placeholders = textBlocks
            .Where(block => block.Text is "--" or "—" or "â€”")
            .Take(2)
            .ToList();
        if (placeholders.Count > 0)
            placeholders[0].Text = profile.InterfaceAddress;
        if (placeholders.Count > 1)
            placeholders[1].Text = endpoint;

        var allowedIps = textBlocks.FirstOrDefault(block => block.Text == "No routing rules loaded");
        if (allowedIps is not null)
            allowedIps.Text = allowedIpsValue;
    }

    private void ResetProfileCard()
    {
        var textBlocks = FindVisualChildren<TextBlock>(this).ToList();
        var profileName = textBlocks.FirstOrDefault(block => block.Text == _displayedProfileName);
        if (profileName is not null)
            profileName.Text = "No profile loaded";

        var badge = textBlocks.FirstOrDefault(block => block.Text == _displayedProfileBadge);
        if (badge is not null)
            badge.Text = "PROFILE / --";

        var interfaceAddress = textBlocks.FirstOrDefault(block => block.Text == _displayedInterfaceAddress);
        if (interfaceAddress is not null)
            interfaceAddress.Text = "--";

        var endpoint = textBlocks.FirstOrDefault(block => block.Text == _displayedEndpoint);
        if (endpoint is not null)
            endpoint.Text = "--";

        var allowedIps = textBlocks.FirstOrDefault(block => block.Text == _displayedAllowedIps);
        if (allowedIps is not null)
            allowedIps.Text = "No routing rules loaded";

        _displayedProfileName = null;
        _displayedProfileBadge = null;
        _displayedInterfaceAddress = null;
        _displayedEndpoint = null;
        _displayedAllowedIps = null;
    }

    private async Task RefreshTelemetryAsync(string tunnelName)
    {
        BackendStatisticsResult result;
        try
        {
            result = await _backend.QueryStatisticsAsync(tunnelName);
        }
        catch (Exception)
        {
            SetTelemetry("ERR", "statistics unavailable", "ERR", "statistics unavailable");
            _eventLog.Add(EventSeverity.Warning, "tunnel.statistics_failed", "WireGuard statistics request failed safely.");
            return;
        }

        if (!result.Succeeded || result.Statistics is null)
        {
            SetTelemetry("ERR", result.Error ?? "statistics unavailable", "ERR", "statistics unavailable");
            _eventLog.Add(EventSeverity.Warning, "tunnel.statistics_failed", "WireGuard statistics were unavailable.");
            return;
        }

        var peers = result.Statistics.Peers;
        var latestHandshake = peers
            .Where(peer => peer.LatestHandshake.HasValue)
            .Select(peer => peer.LatestHandshake!.Value)
            .OrderByDescending(value => value)
            .FirstOrDefault();
        var receive = peers.Aggregate(0UL, (total, peer) => checked(total + peer.ReceiveBytes));
        var transmit = peers.Aggregate(0UL, (total, peer) => checked(total + peer.TransmitBytes));

        SetTelemetry(
            latestHandshake == default ? "NONE" : latestHandshake.ToLocalTime().ToString("HH:mm:ss"),
            $"{peers.Count} peer(s) // latest",
            $"{FormatBytes(transmit)} / {FormatBytes(receive)}",
            "tx / rx");
    }

    private async Task<(bool Succeeded, string? Error)> WaitForBackendStatusAsync(
        string tunnelName,
        WireGuardServiceStatus expectedStatus)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var status = await _backend.QueryStatusAsync(tunnelName);
            if (status.Succeeded && status.Status == expectedStatus)
                return (true, null);

            if (!status.Succeeded && attempt == 9)
                return (false, status.Error);

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return (false, $"WireGuard service did not reach {expectedStatus} state in time.");
    }

    private async void TelemetryTimer_Tick(object? sender, EventArgs e)
    {
        if (_telemetryRefreshInFlight || _tunnelState.State != TunnelState.Connected || _backend is DemoWireGuardBackend)
            return;

        _telemetryRefreshInFlight = true;
        try
        {
            await RefreshTelemetryAsync(_activeProfile?.Name ?? "demo");
        }
        finally
        {
            _telemetryRefreshInFlight = false;
        }
    }

    private void ResetTelemetry() => SetTelemetry("—", "waiting for tunnel", "—", "up / down");

    private void SetTelemetry(string handshake, string handshakeDetail, string transfer, string transferDetail)
    {
        var metricBlocks = FindVisualChildren<TextBlock>(this)
            .Where(block => block.Text is "—" or "â€”" || block.Text == _displayedHandshake || block.Text == _displayedTransfer)
            .ToList();
        if (metricBlocks.Count >= 2)
        {
            metricBlocks[^2].Text = handshake;
            metricBlocks[^1].Text = transfer;
        }

        var waitingBlock = FindVisualChildren<TextBlock>(this).FirstOrDefault(block => block.Text == "waiting for tunnel" || block.Text == _displayedHandshakeDetail);
        if (waitingBlock is not null)
            waitingBlock.Text = handshakeDetail;

        var transferBlock = FindVisualChildren<TextBlock>(this).FirstOrDefault(block => block.Text is "up / down" or "tx / rx" or "statistics unavailable" || block.Text == _displayedTransferDetail);
        if (transferBlock is not null)
            transferBlock.Text = transferDetail;

        _displayedHandshake = handshake;
        _displayedTransfer = transfer;
        _displayedHandshakeDetail = handshakeDetail;
        _displayedTransferDetail = transferDetail;
    }

    private static string FormatBytes(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
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
