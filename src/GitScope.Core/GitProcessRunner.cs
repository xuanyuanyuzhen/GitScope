using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace GitScope.Core;

public sealed record GitResult(int ExitCode, byte[] Output, string Error)
{
    public string Text => Encoding.UTF8.GetString(Output);
}

// All production Git access uses this boundary. No shell, replacement objects, prompts or transport.
public class GitProcessRunner
{
    public string Executable { get; }
    public GitProcessRunner(string executable = "git") => Executable = executable;

    internal Process Start(string directory, IReadOnlyList<string> arguments, bool input = false)
    {
        var info = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            RedirectStandardInput = input, StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = directory
        };
        foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
            info.Environment.Remove(key);
        info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        info.Environment["GIT_CONFIG_GLOBAL"] = "NUL";
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        info.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        info.Environment["GIT_NO_REPLACE_OBJECTS"] = "1";
        info.Environment["GIT_NO_LAZY_FETCH"] = "1";
        info.Environment["GIT_ALLOW_PROTOCOL"] = "";
        info.Environment["LC_ALL"] = "C.UTF-8";
        foreach (var arg in new[] { "--no-pager", "--no-replace-objects", "--no-optional-locks", "-c", "core.quotepath=false", "-c", "core.fsmonitor=false", "-c", "core.hooksPath=NUL", "-c", "protocol.allow=never", "-C", directory })
            info.ArgumentList.Add(arg);
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        var process = new Process { StartInfo = info };
        try
        {
            if (!process.Start()) throw new ScopeException(ErrorCode.GitUnavailable, "无法启动 Git。");
            return process;
        }
        catch (Win32Exception e)
        {
            process.Dispose();
            throw new ScopeException(ErrorCode.GitUnavailable, "未找到可用的 Git，请检查本机安装与 PATH。", e);
        }
    }

    public virtual async Task<GitResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Start(workingDirectory, arguments);
        using var registration = cancellationToken.Register(() => Kill(process));
        using var output = new MemoryStream();
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new(process.ExitCode, output.ToArray(), Redact(await error.ConfigureAwait(false)));
        }
        catch
        {
            Kill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await error.ConfigureAwait(false);
            throw;
        }
    }

    public virtual async Task<GitResult> RequireAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(workingDirectory, arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
            throw new ScopeException(ErrorCode.GitCommandFailed, $"Git 读取失败（退出码 {result.ExitCode}）：{result.Error.Trim()}");
        return result;
    }

    internal static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }

    public static string Redact(string message)
    {
        var redacted = Regex.Replace(message, @"(?i)(https?://)[^\s/@]+:[^\s/@]+@", "$1[redacted]@");
        redacted = Regex.Replace(redacted, @"(?i)(token|password|authorization|access_token)(\s*[=:]\s*)[^\s&]+", "$1$2[redacted]");
        return redacted.Length > 8192 ? redacted[..8192] + "\n[stderr truncated]" : redacted;
    }
}
