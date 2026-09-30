using System.Text.RegularExpressions;

namespace Shigure;

internal static class AddonLuaNames
{
    public static string Assignment(string source, string member)
    {
        var match = Regex.Match(source,
            @"(?m)^[ \t]*([A-Za-z_][A-Za-z0-9_]*)\.(?:ClassBlocks|ClassMacros)[ \t]*=");
        var addon = match.Success ? match.Groups[1].Value : "Fuyutsui";
        return addon + "." + member;
    }
}
