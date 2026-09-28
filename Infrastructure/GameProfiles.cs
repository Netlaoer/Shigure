using System.Text.Json;

namespace Shigure;

internal sealed record GameProfile(string ProcessName, string AddonName, string BaseDirectory)
{
    public string AddonRoot => Path.Combine(BaseDirectory, AddonName);
    public string RuntimeDirectory => AddonName.Equals("Fuyutsui", StringComparison.OrdinalIgnoreCase)
        ? BaseDirectory
        : Path.Combine(BaseDirectory, "profiles", AddonName);
    public string ModuleDirectory => AddonName.Equals("Fuyutsui", StringComparison.OrdinalIgnoreCase)
        ? ModuleStore.ResolveModuleDirectory()
        : Path.Combine(AppPaths.UserDataDirectory, "module-" + AddonName);
}

internal sealed class GameProfiles
{
    private sealed record ProfileEntry(string Process, string Addon);
    private sealed record ProfileFile(ProfileEntry[] Profiles);

    private readonly Dictionary<string, GameProfile> _byProcess;
    private readonly GameProfile _default;

    private GameProfiles(Dictionary<string, GameProfile> byProcess, GameProfile defaultProfile)
    {
        _byProcess = byProcess;
        _default = defaultProfile;
    }

    public GameProfile Default => _default;
    public IReadOnlyList<string> ProcessNames => _byProcess.Keys.ToArray();
    public IReadOnlyList<GameProfile> DistinctAddons => _byProcess.Values
        .DistinctBy(profile => profile.AddonName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public GameProfile? Find(string? processName)
        => processName is not null && _byProcess.TryGetValue(processName, out var profile) ? profile : null;

    public static GameProfiles Load(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "game_profiles.json");
        if (!File.Exists(path))
        {
            var legacyNames = File.Exists(Path.Combine(baseDirectory, "wow_process.txt"))
                ? File.ReadAllLines(Path.Combine(baseDirectory, "wow_process.txt"))
                : ["Wow"];
            var fallback = legacyNames.Select(name => name.Trim())
                .Where(name => name.Length > 0 && !name.StartsWith('#') && !name.StartsWith(';'))
                .Select(name => new ProfileEntry(name, "Fuyutsui"))
                .ToArray();
            return Create(baseDirectory, fallback);
        }

        var file = JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        });
        return Create(baseDirectory, file?.Profiles ?? []);
    }

    private static GameProfiles Create(string baseDirectory, IEnumerable<ProfileEntry> entries)
    {
        var byProcess = new Dictionary<string, GameProfile>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var process = entry.Process?.Trim() ?? string.Empty;
            var addon = entry.Addon?.Trim() ?? string.Empty;
            if (process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                process = process[..^4];
            }
            if (process.Length == 0 || addon.Length == 0
                || process.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || addon.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || addon is "." or ".." || byProcess.ContainsKey(process))
            {
                throw new InvalidDataException("game_profiles.json 包含无效或重复的进程/插件名称。");
            }
            byProcess.Add(process, new GameProfile(process, addon, baseDirectory));
        }
        if (byProcess.Count == 0)
        {
            throw new InvalidDataException("game_profiles.json 未配置游戏进程。");
        }
        var defaultProfile = byProcess.GetValueOrDefault("Wow") ?? byProcess.Values.First();
        return new GameProfiles(byProcess, defaultProfile);
    }
}

internal sealed class ActiveGameProfile(GameProfile profile)
{
    public GameProfile Current { get; set; } = profile;
}
