#!/usr/bin/env dotnet-script
#load "nuget:Dotnet.Build, 0.3.9"
#load "nuget:dotnet-steps, 0.0.1"
#load "nuget:github-changelog, 0.1.5"
#load "build-context.csx"
using static ChangeLog;
using static ReleaseManagement;


Step test = () => DotNet.Test(UnitTests);

Step pack = () => {
    test();
    DotNet.Pack(projectFolder, nuGetArtifactsFolder);
};

AsyncStep release = async () => {
    pack();
    await Deploy();
};

await StepRunner.Execute(Args);



private async Task Deploy()
{
    if (!BuildEnvironment.IsSecure)
    {
        WriteLine("Deployment can only be done from within a secure build environment");
        return;
    }

    Logger.Log("Creating release notes");
    var generator = ChangeLogFrom(owner, projectName, BuildEnvironment.GitHubAccessToken).SinceLatestTag();
    if (!Git.Default.IsTagCommit())
    {
        generator = generator.IncludeUnreleased();
    }
    await generator.Generate(PathToReleaseNotes);


    if (Git.Default.IsTagCommit())
    {
        Git.Default.RequreCleanWorkingTree();
        var releaseManager = ReleaseManagerFor(owner, projectName, BuildEnvironment.GitHubAccessToken);
        await releaseManager.CreateRelease(Git.Default.GetLatestTag(),PathToReleaseNotes, Array.Empty<ReleaseAsset>());
        NuGet.Push(nuGetArtifactsFolder);
    }
}
