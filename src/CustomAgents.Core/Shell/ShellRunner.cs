using System.Diagnostics;
using System.Text;

namespace CustomAgents.Core.Shell;

public sealed class ShellRunner
{
    public async Task<string> RunAsync(string command, string workingDirectory, CancellationToken cancellationToken = default)
    {
        command = CommandUnescape.UnescapeFully(command);

        var psi = new ProcessStartInfo
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ConfigureShell(psi, command);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
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

        if (builder.Length == 0)
        {
            builder.Append($"[exit code {process.ExitCode}]");
        }

        return builder.ToString();
    }

    private static void ConfigureShell(ProcessStartInfo psi, string command)
    {
        if (OperatingSystem.IsWindows())
        {
            psi.FileName = "cmd.exe";
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(command);
            return;
        }

        psi.FileName = "/bin/bash";
        psi.ArgumentList.Add("-lc");
        psi.ArgumentList.Add(command);
    }
}
