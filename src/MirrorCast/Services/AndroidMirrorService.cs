using System.Diagnostics;
using System.IO;
using MirrorCast.Models;

namespace MirrorCast.Services;

public sealed class AndroidMirrorService : IDisposable
{
    public const string WindowTitle = "MirrorCast · 安卓投屏";
    private Process? _scrcpyProcess;

    public bool IsAvailable => ResolveTool("scrcpy.exe", "scrcpy") != null
        && ResolveTool("adb.exe", "adb") != null;

    public async Task<IReadOnlyList<AndroidDeviceInfo>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        var adb = ResolveTool("adb.exe", "adb")
            ?? throw new InvalidOperationException("未找到 ADB，请重新安装完整版或配置 Android Platform-Tools。");

        // Android 11+ publishes the current wireless-debugging port over mDNS.
        // Connecting is best-effort; USB and manually connected devices still work.
        var mdns = await RunAsync(adb, ["mdns", "services"], cancellationToken);
        foreach (var endpoint in ParseMdnsEndpoints(mdns.Output))
            _ = await RunAsync(adb, ["connect", endpoint], cancellationToken);

        var result = await RunAsync(adb, ["devices", "-l"], cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? "无法读取安卓设备列表。" : result.Error.Trim());

        return ParseDevices(result.Output);
    }

    public async Task<IntPtr> StartAsync(AndroidMirrorOptions options, CancellationToken cancellationToken = default)
    {
        Stop();
        var scrcpy = ResolveTool("scrcpy.exe", "scrcpy")
            ?? throw new InvalidOperationException("未找到 scrcpy，请重新安装 MirrorCast 完整版。");
        var adb = ResolveTool("adb.exe", "adb")
            ?? throw new InvalidOperationException("未找到 ADB，请重新安装 MirrorCast 完整版。");

        if (!string.IsNullOrWhiteSpace(options.Address))
        {
            if (!IsValidAddress(options.Address))
                throw new InvalidOperationException("无线地址格式不正确。");
            var connected = await RunAsync(adb, ["connect", $"{options.Address.Trim()}:{options.Port}"], cancellationToken);
            if (connected.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(connected.Error) ? "无线 ADB 连接失败。" : connected.Error.Trim());
        }

        var args = new List<string>
        {
            $"--window-title={WindowTitle}",
            "--max-size=1920",
            $"--max-fps={Math.Clamp(options.MaxFps, 15, 240)}",
            options.Audio ? "--audio-source=output" : "--no-audio"
        };
        if (!options.Control) args.Add("--no-control");
        if (options.TurnScreenOff) args.Add("--turn-screen-off");
        if (!string.IsNullOrWhiteSpace(options.Serial)) args.Add($"--serial={options.Serial}");
        else if (!string.IsNullOrWhiteSpace(options.Address)) args.Add($"--serial={options.Address.Trim()}:{options.Port}");
        else args.Add("--select-usb");

        var startInfo = CreateStartInfo(scrcpy, args);
        startInfo.WorkingDirectory = Path.GetDirectoryName(scrcpy)!;
        startInfo.Environment["ADB"] = adb;
        var server = ResolveBundledPath("scrcpy", "scrcpy-server");
        if (server != null) startInfo.Environment["SCRCPY_SERVER_PATH"] = server;

        _scrcpyProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("scrcpy 进程启动失败。");

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_scrcpyProcess.HasExited)
                throw new InvalidOperationException("scrcpy 在创建投屏窗口前退出，请确认手机已授权 USB/无线调试。");
            _scrcpyProcess.Refresh();
            if (_scrcpyProcess.MainWindowHandle != IntPtr.Zero)
                return _scrcpyProcess.MainWindowHandle;
            await Task.Delay(120, cancellationToken);
        }

        Stop();
        throw new TimeoutException("等待安卓投屏窗口超时，请检查设备连接和授权状态。");
    }

    public void Stop()
    {
        var process = _scrcpyProcess;
        _scrcpyProcess = null;
        if (process == null) return;
        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(1200)) process.Kill(true);
            }
        }
        catch { }
        finally { process.Dispose(); }
    }

    private static IReadOnlyList<AndroidDeviceInfo> ParseDevices(string output)
    {
        var devices = new List<AndroidDeviceInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || parts[1] != "device") continue;
            var serial = parts[0];
            var model = parts.FirstOrDefault(value => value.StartsWith("model:"))?[6..].Replace('_', ' ') ?? "安卓设备";
            devices.Add(new AndroidDeviceInfo
            {
                Serial = serial,
                Name = model,
                Transport = serial.Contains(':') ? "Wi-Fi" : "USB"
            });
        }
        return devices.OrderBy(device => device.Name).ToList();
    }

    private static IEnumerable<string> ParseMdnsEndpoints(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[1].Contains("_adb-tls-connect._tcp") && parts[2].Contains(':'))
                yield return parts[2];
        }
    }

    private static bool IsValidAddress(string value) => value.Length <= 253
        && value.All(character => char.IsAsciiLetterOrDigit(character) || ".:-_[]".Contains(character));

    private static string? ResolveTool(string bundledName, string pathName)
    {
        var bundled = ResolveBundledPath(pathName == "adb" ? "platform-tools" : "scrcpy", bundledName);
        if (bundled != null) return bundled;
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory.Trim(), bundledName))
            .FirstOrDefault(File.Exists);
    }

    private static string? ResolveBundledPath(string directory, string file)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Resources", "android-tools", "windows-x86_64", directory, file),
            Path.Combine(AppContext.BaseDirectory, "android-tools", "windows-x86_64", directory, file)
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string executable, IEnumerable<string> args, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo(executable, args) };
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static ProcessStartInfo CreateStartInfo(string executable, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    public void Dispose() => Stop();
}
