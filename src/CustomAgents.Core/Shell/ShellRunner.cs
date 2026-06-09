using System.Diagnostics;
using System.Text;

namespace CustomAgents.Core.Shell;

public sealed class ShellRunner
{
    public Task<ShellRunResult> RunAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken = default) =>
        RunAsync(command, workingDirectory, timeout: null, cancellationToken);

    public async Task<ShellRunResult> RunAsync(
        string command,
        string workingDirectory,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        command = CommandUnescape.UnescapeFully(command);

        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ConfigureChildEncoding(psi);
        ConfigureShell(psi, command);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = timeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        if (timeout is { } timeoutValue)
        {
            timeoutCts!.CancelAfter(timeoutValue);
        }

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts?.Token ?? cancellationToken);
        }
        catch (OperationCanceledException) when (timeout.HasValue && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            try
            {
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var output = FormatOutput(stdout, stderr, timedOut ? null : process.ExitCode);

        return new ShellRunResult(output, timedOut);
    }

    private static string FormatOutput(string stdout, string stderr, int? exitCode)
    {
        var builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            builder.Append(stdout.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(stderr.TrimEnd());
        }

        if (builder.Length == 0 && exitCode is int code)
        {
            builder.Append($"[exit code {code}]");
        }

        return builder.ToString();
    }

    private static void ConfigureChildEncoding(ProcessStartInfo psi)
    {
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";
    }

    private static void ConfigureShell(ProcessStartInfo psi, string command)
    {
        if (OperatingSystem.IsWindows())
        {
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add($"chcp 65001 >nul & {command}");
            return;
        }

        psi.FileName = "/bin/bash";
        psi.ArgumentList.Add("-lc");
        psi.ArgumentList.Add(command);
    }
}
