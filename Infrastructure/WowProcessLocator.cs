using System.Diagnostics;
using System.Text;

namespace Shigure;

/// <summary>
/// 按 game_profiles.json 中的进程名，从 Windows Z 顺序顶部查找第一个可见顶层窗口。
/// 每次查询都会重新检查窗口顺序，以便运行期间切换游戏窗口。
/// </summary>
internal sealed class WowProcessLocator
{
    private readonly GameProfiles _profiles;
    private readonly string? _boundProcessName;

    public WowProcessLocator(GameProfiles profiles, string? boundProcessName = null)
    {
        _profiles = profiles;
        _boundProcessName = boundProcessName;
    }

    public WowProcessLocator ForProcess(string processName)
        => new(_profiles, processName);

    public nint FindFrontmostWindow()
    {
        if (_boundProcessName is not null)
        {
            var frontmost = new WowProcessLocator(_profiles);
            var hwnd = frontmost.FindFrontmostWindow();
            return string.Equals(GetProcessName(hwnd), _boundProcessName, StringComparison.OrdinalIgnoreCase)
                ? hwnd
                : 0;
        }
        var processIds = GetCandidateProcessIds();
        if (processIds.Count == 0)
        {
            return 0;
        }

        nint foundWindow = 0;
        _ = NativeMethods.EnumWindows((hwnd, lParam) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
            {
                return true;
            }

            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
            if (!processIds.Contains(processId))
            {
                return true;
            }

            foundWindow = hwnd;
            return false;
        }, 0);

        return foundWindow;
    }

    public string? FindFrontmostProcessPath()
    {
        var hwnd = FindFrontmostWindow();
        if (hwnd == 0)
        {
            return null;
        }

        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        return processId == 0 ? null : TryGetProcessPath(processId);
    }

    public string? FindFrontmostProcessName()
    {
        return GetProcessName(FindFrontmostWindow());
    }

    private static string? GetProcessName(nint hwnd)
    {
        if (hwnd == 0)
        {
            return null;
        }
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            return null;
        }
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public string DescribeConfiguredProcesses()
    {
        var names = ReadProcessNames();
        return names.Count == 0 ? "未配置" : string.Join("、", names);
    }

    private HashSet<uint> GetCandidateProcessIds()
    {
        var result = new HashSet<uint>();
        foreach (var processName in ReadProcessNames())
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        result.Add(unchecked((uint)process.Id));
                    }
                    catch (InvalidOperationException)
                    {
                        // 进程可能在枚举期间退出。
                    }
                }
            }
        }

        return result;
    }

    private IReadOnlyList<string> ReadProcessNames()
    {
        if (_boundProcessName is not null)
        {
            return [_boundProcessName];
        }
        return _profiles.ProcessNames;
    }

    private static string? TryGetProcessPath(uint processId)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
        if (handle == 0)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = buffer.Capacity;
            return NativeMethods.QueryFullProcessImageName(handle, 0, buffer, ref size) && size > 0
                ? buffer.ToString()
                : null;
        }
        finally
        {
            _ = NativeMethods.CloseHandle(handle);
        }
    }
}
