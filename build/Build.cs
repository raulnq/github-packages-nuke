using Nuke.Common;
using Nuke.Common.IO;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.NuGet;
using Serilog;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.CI.GitHubActions;

[GitHubActions(
    "Push",
    GitHubActionsImage.UbuntuLatest,
    On = new[] { GitHubActionsTrigger.WorkflowDispatch },
    InvokedTargets = new[] { nameof(Push) }, EnableGitHubToken = true, AutoGenerate = false)]
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

    [GitVersion]
    readonly GitVersion GitVersion;

    AbsolutePath PackagesDirectory => RootDirectory / "packages";

    Target Clean => _ => _
        .Executes(() =>
        {
            PackagesDirectory.CreateOrCleanDirectory();
        });

    Target Pack => _ => _
        .DependsOn(Clean)
        .DependsOn(AddSource)
        .Executes(() =>
        {
            DotNetPack(s => s
            .SetProject(RootDirectory / "MyLib")
            .SetOutputDirectory(PackagesDirectory)
            .SetPackageProjectUrl($"https://github.com/{GitHubUser}/github-packages-nuke")
            .SetVersion(GitVersion.SemVer)
            .SetPackageId("MyLib")
            .SetAuthors($"{GitHubUser}")
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
               .EnableStorePasswordInClearText()
               .SetSource($"https://nuget.pkg.github.com/{GitHubUser}/index.json"));
            }
            catch
            {
                Log.Information("Source already added");
            }
           ;
        });

}
