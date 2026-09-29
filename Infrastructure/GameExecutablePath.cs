namespace Shigure;

internal static class GameExecutablePath
{
    public static bool TryValidate(
        string? path, string expectedFileName, out string fullPath, out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "尚未选择游戏程序。";
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = "游戏程序路径无效。";
            return false;
        }

        if (!string.Equals(Path.GetFileName(fullPath), expectedFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            error = $"请选择 {expectedFileName}。";
            return false;
        }

        if (!File.Exists(fullPath))
        {
            error = $"找不到游戏程序：{fullPath}";
            return false;
        }

        return true;
    }
}
