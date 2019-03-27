#!/usr/bin/env dotnet-script
#load "nuget:Dotnet.Build, 0.3.9"
#load "nuget:dotnet-steps, 0.0.1"
#load "nuget:github-changelog, 0.1.5"
#load "build-context.csx"


Console.WriteLine("Hello world!");

Step test = () => DotNet.Test(UnitTests);

[DefaultStep]
Step pack = () => DotNet.Pack(projectFolder, nuGetArtifactsFolder);

await StepRunner.Execute(Args);