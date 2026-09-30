namespace Shigure;

/// <summary>一个插件版本的项目文件、模块快照和依赖服务。</summary>
internal sealed class GameWorkspace
{
    public GameWorkspace(GameProfile profile)
    {
        Profile = profile;
        Modules = new ModuleStore(profile.ModuleDirectory);
        Dependencies = new ModuleDependencyService(profile.AddonRoot);
        Modules.SetImportIssues(Modules.GetModulesForDisplay().Select(module => module.Id), []);
    }

    public GameProfile Profile { get; }
    public ModuleStore Modules { get; }
    public ModuleDependencyService Dependencies { get; }
    public bool HasValidatedModules { get; set; }
    public bool NeedsImport { get; set; }
    public bool ResumingImport { get; set; }
}
