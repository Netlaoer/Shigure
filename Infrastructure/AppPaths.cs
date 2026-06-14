namespace Shigure;

internal static class AppPaths
{
    public const string OriginalBaseDirectoryEnvironmentKey = "SHIGURE_ORIGINAL_BASE_DIRECTORY";
    public const string RandomizedDisplayNameEnvironmentKey = "SHIGURE_RANDOMIZED_DISPLAY_NAME";

    public static string BaseDirectory
    {
        get
        {
            var originalDirectory = Environment.GetEnvironmentVariable(OriginalBaseDirectoryEnvironmentKey);
            return string.IsNullOrWhiteSpace(originalDirectory)
                ? AppContext.BaseDirectory
                : EnsureTrailingSeparator(originalDirectory);
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            || fullPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }
}
