using System.Text.Json;

namespace Shigure;

internal sealed record GameProfile(string ProcessName, string AddonName, string Version, string BaseDirectory)
{
    public string RuntimeDirectory => Path.Combine(BaseDirectory, Version);
    public string AddonRoot => Path.Combine(RuntimeDirectory, AddonName);
    public string ModuleDirectory => AddonName.Equals("Fuyutsui", StringComparison.OrdinalIgnoreCase)
        ? ModuleStore.ResolveModuleDirectory()
        : Path.Combine(AppPaths.UserDataDirectory, "module-" + AddonName);
}

internal sealed class GameProfiles
{
    private sealed record ProfileEntry(string Process, string Addon, string? Version);
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

    public GameProfile? FindByAddon(string? addonName)
        => addonName is null
            ? null
            : _byProcess.Values.FirstOrDefault(profile =>
                string.Equals(profile.AddonName, addonName, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<GameProfile> FindAllByAddon(string addonName)
        => _byProcess.Values.Where(profile =>
            string.Equals(profile.AddonName, addonName, StringComparison.OrdinalIgnoreCase)).ToArray();

    public static GameProfiles Load(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "game_profiles.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("找不到游戏配置 game_profiles.json。", path);
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
            var version = entry.Version?.Trim();
            version ??= addon.Equals("Shingen", StringComparison.OrdinalIgnoreCase) ? "Forever" : "Retail";
            if (version.Equals("Retail", StringComparison.OrdinalIgnoreCase))
            {
                version = "Retail";
            }
            else if (version.Equals("Forever", StringComparison.OrdinalIgnoreCase))
            {
                version = "Forever";
            }
            if (process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                process = process[..^4];
            }
            if (process.Length == 0 || addon.Length == 0
                || process.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || addon.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || addon is "." or ".."
                || version is not ("Retail" or "Forever")
                || byProcess.ContainsKey(process))
            {
                throw new InvalidDataException("game_profiles.json 包含无效或重复的进程/插件名称。");
            }
            byProcess.Add(process, new GameProfile(process, addon, version, baseDirectory));
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
