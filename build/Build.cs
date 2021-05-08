using System.Collections.Generic;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AppVeyor;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[ShutdownDotNetAfterServerBuild]
partial class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution(GenerateProjects = true)] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    [CI] readonly AppVeyor AppVeyor;
    [CI] readonly GitHubActions GitHubActions;
    IBuildServer BuildServer => (IBuildServer) AppVeyor ?? GitHubActions;
    
    const string MasterBranch = "master";
    const string DevBranch = "dev";
    const string ReleaseBranchPrefix = "rel";

    bool IsReleaseBranch => BuildServer?.Branch == MasterBranch || BuildServer?.Branch?.StartsWith(ReleaseBranchPrefix) == true;
    
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    string PublicNuGetSource => "https://api.nuget.org/v3/index.json";

    string GitHubRegistrySource => GitHubActions != null
        ? $"https://nuget.pkg.github.com/{GitHubActions.GitHubRepositoryOwner}/index.json"
        : null;
    
    [Parameter] [Secret] readonly string PublicNuGetApiKey;
    [Parameter] [Secret] readonly string PublicMyGetApiKey;
    [Parameter] [Secret] readonly string GitHubRegistryApiKey;
    
    protected override void OnBuildInitialized()
    {
        if (IsReleaseBranch)
        {
            // Ensure we are not using the myget feed for dependencies
            CopyFile("NuGet.release.config", "NuGet.config");
        }
        SetVariable("BuildNumber", GitHubActions?.GitHubRunNumber ?? AppVeyor?.BuildNumber.ToString());
    }

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            WorkingDirectory.GlobDirectories("*/bin", "*/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    
    Target Pack => _ => _
        .DependsOn(Restore)
        .After(Test)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetProject(Solution.Jint)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .EnableNoRestore());
        });
    
    bool IsOriginalRepository => GitRepository.Identifier == "sebastienros/jint";
    string NuGetApiKey => IsOriginalRepository ? PublicNuGetApiKey : GitHubRegistryApiKey;
    string NuGetSource => IsOriginalRepository ? PublicNuGetSource : GitHubRegistrySource;
    
    IEnumerable<AbsolutePath> PushPackageFiles => ArtifactsDirectory.GlobFiles("*.nupkg");
    
    Target Publish => _ => _
        .Consumes(Pack)
        .Requires(() => IsOriginalRepository && AppVeyor != null && (GitRepository.IsOnMasterBranch() || GitRepository.IsOnReleaseBranch()) ||
                        !IsOriginalRepository)
        .WhenSkipped(DependencyBehavior.Execute)
        .Executes(() =>
        {
            DotNetNuGetPush(_ => _
                .SetSource(NuGetSource)
                .SetApiKey(NuGetApiKey)
                .CombineWith(PushPackageFiles, (_, v) => _
                    .SetTargetPath(v)
                )
            );
        });
}