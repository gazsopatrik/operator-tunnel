using OperatorTunnel.Audit;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace OperatorTunnel.App;

public partial class AuditTerminalWindow : Window
{
    private readonly IAuditObservationStore _observationStore;
    private readonly IAuditEvidenceStore _evidenceStore;
    private readonly AuditSession _session;
    private CancellationTokenSource? _runCancellation;

    public AuditTerminalWindow(IAuditObservationStore observationStore, IAuditEvidenceStore evidenceStore, AuditSession session)
    {
        _observationStore = observationStore ?? throw new ArgumentNullException(nameof(observationStore));
        _evidenceStore = evidenceStore ?? throw new ArgumentNullException(nameof(evidenceStore));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        InitializeComponent();
        SessionLabel.Text = $"// SESSION {_session.Id[..8].ToUpperInvariant()}";
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runCancellation is not null)
        {
            StatusText.Text = "scan already running";
            return;
        }

        var targets = TargetsTextBox.Text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        try
        {
            var command = NmapCommandBuilder.Build(targets);
            _runCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            StatusText.Text = "running // nmap structured XML scan";
            OutputTextBox.Clear();
            var result = await RunProcessAsync(command, _runCancellation.Token);
            OutputTextBox.Text = result.Output + (string.IsNullOrWhiteSpace(result.Error) ? string.Empty : $"\n\n[stderr]\n{result.Error}");
            if (result.ExitCode != 0)
            {
                StatusText.Text = $"scan failed // exit code {result.ExitCode}";
                return;
            }

            var evidence = AuditEvidence.Create(_session.Id, "nmap", "nmap-live.xml", result.Output);
            var parsed = new NmapXmlParser().Parse(result.Output, _session.Id, evidence.Id, evidence.CapturedAt);
            if (!parsed.IsValid)
            {
                StatusText.Text = $"scan output rejected // {parsed.Issues[0]}";
                return;
            }

            await _evidenceStore.SaveAsync(evidence);
            await _observationStore.AddAsync(parsed.Observations);
            StatusText.Text = $"scan imported // {parsed.Observations.Count} observations // evidence saved";
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = $"target rejected // {ex.Message}";
        }
        catch (Win32Exception)
        {
            StatusText.Text = "nmap unavailable // install Nmap or use XML import";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "scan cancelled or timed out";
        }
        catch (IOException)
        {
            StatusText.Text = "terminal process failed safely";
        }
        finally
        {
            _runCancellation?.Dispose();
            _runCancellation = null;
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessAsync(AuditExternalCommand command, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command.FileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        foreach (var argument in command.Arguments)
            process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(output, error);
        return (process.ExitCode, await output, await error);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _runCancellation?.Cancel();
        Close();
    }
}
