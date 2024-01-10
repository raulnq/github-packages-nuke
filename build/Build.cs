using Nuke.Common;
using Nuke.Common.IO;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using Serilog;
using static Nuke.Common.Tools.GitVersion.GitVersionTasks;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.CI.GitHubActions;
using System.Numerics;

[GitHubActions(
    "Push",
    GitHubActionsImage.UbuntuLatest,
    OnPushBranches = new[] { "main" },
    InvokedTargets = new[] { nameof(Push) }, EnableGitHubToken = true)]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Push);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    private string Version;

    Target GetVersion => _ => _
        .Executes(() =>
        {
            var (result, _) = GitVersion();
            Version = result.FullSemVer;
        });

    AbsolutePath PackagesDirectory => RootDirectory / "packages";

    Target Clean => _ => _
        .Executes(() =>
        {
            PackagesDirectory.CreateOrCleanDirectory();
        });

    Target Pack => _ => _
        .DependsOn(Clean)
        .DependsOn(AddSource)
        .DependsOn(GetVersion)
        .Produces(PackagesDirectory / "*.nupkg")
        .Executes(() =>
        {
            DotNetPack(s => s
            .SetProject(RootDirectory / "MyLib")
            .SetOutputDirectory(PackagesDirectory)
            .SetPackageProjectUrl($"https://github.com/{GitHubUser}/github-packages-nuke")
            .SetVersion(Version)
            .SetPackageId("MyLib")
            .SetAuthors("raulnq")
            .SetDescription("MyLib nuget package")
            .SetConfiguration(Configuration));
        });

    Target Push => _ => _
        .DependsOn(Pack)  
        .Executes(() =>
        {
            DotNetNuGetPush(s => s
            .SetTargetPath(PackagesDirectory / "*.nupkg")
            .SetApiKey(GitHubToken)
            .SetSource("github"));
        });

    [Parameter()]
    readonly string GitHubUser = GitHubActions.Instance?.RepositoryOwner;

    [Parameter()]
    [Secret] 
    readonly string GitHubToken;

    Target AddSource => _ => _      
        .Requires(() => GitHubUser)
        .Requires(() => GitHubToken)
        .Executes(() =>
        {
            try
            {
                DotNetNuGetAddSource(s => s
               .SetName("github")
               .SetUsername(GitHubUser)
               .SetPassword(GitHubToken)
               .SetSource($"https://nuget.pkg.github.com/{GitHubUser}/index.json"));
            }
            catch
            {
                Log.Information("Source already added");
            }
           ;
        });

}