using System.Diagnostics;

namespace OperatorTunnel.Core.Backend;

/// <summary>
/// Runs an allowlisted external command without invoking cmd.exe or PowerShell.
/// The caller remains responsible for deciding which executable is trusted.
/// </summary>
public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessExecutionResult> RunAsync(
        ExternalProcessCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.FileName);

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

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(standardOutput, standardError);
        }
        catch
        {
            TryTerminate(process);
            throw;
        }

        return new ProcessExecutionResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and Kill.
        }
    }
}

