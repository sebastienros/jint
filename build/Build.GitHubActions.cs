using Nuke.Common;
using Nuke.Common.CI.GitHubActions;

[GitHubActions(
    name: "continuous",
    GitHubActionsImage.WindowsLatest,
    GitHubActionsImage.UbuntuLatest,
    GitHubActionsImage.MacOsLatest,
    AutoGenerate = true,
    OnPushBranchesIgnore = new[] { MasterBranch, DevBranch, ReleaseBranchPrefix + "/*" },
    InvokedTargets = new[] { nameof(Test), nameof(Pack) },
    ImportSecrets = new[] {"TODO"},
    PublishArtifacts = true,
    CacheKeyFiles = new[] { "global.json", "**/*.csproj" }
)]
partial class Build
{
}