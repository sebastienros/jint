using Nuke.Common.CI.AppVeyor;

/* TODO this would replace current appveyor config */ 
[AppVeyor(
    suffix: null,
    AppVeyorImage.VisualStudio2019,
    BranchesOnly = new[] { MasterBranch, DevBranch, "/" + ReleaseBranchPrefix + "\\/*/" },
    SkipTags = true,
    InvokedTargets = new[] { nameof(Pack), nameof(Test), nameof(Publish) },
    AutoGenerate = false, // TODO true
    SkipBranchesWithPullRequest = true,
    Init = new[]
    {
        "git config --global core.autocrlf true"
    }
)]
[AppVeyor(
    suffix: "continuous",
    AppVeyorImage.VisualStudio2019,
    AppVeyorImage.Ubuntu1804,
    BranchesExcept = new[] { MasterBranch, DevBranch, "/" + ReleaseBranchPrefix + "\\/*/" },
    SkipTags = true,
    InvokedTargets = new[] { nameof(Test), nameof(Pack) },
    Init = new[]
    {
        "git config --global core.autocrlf true"
    }
)]
partial class Build
{
    public static class AppVeyorSecrets
    {
        public const string MyGetGetApiKey = MyGetApiKeyName + ":" + MyGetApiKeyValue;
        const string MyGetApiKeyName = nameof(PublicMyGetApiKey);
        const string MyGetApiKeyValue = "7PQvuxXn5P39X5QDlDKWbNpOKJKivpqkq7umakIirAZ12CSTAiCwjtJhSBGVboPm";

        public const string NuGetApiKey = NuGetApiKeyName + ":" + NuGetApiKeyValue;
        const string NuGetApiKeyName = nameof(PublicNuGetApiKey);
        const string NuGetApiKeyValue = "qZ6R8U4mtBXFVRhhNLJyRz3bktF/jL5BvzrCQsXcn6ATRQ4YavFP3By8Sg4hYMH5";
    }
}