#load "nuget:Dotnet.Build, 0.3.9"
using static FileUtils;
using System.Xml.Linq;

var owner = "seesharper";
var projectName = "ResourceReader";
var root = FileUtils.GetScriptFolder();
var solutionFolder = Path.Combine(root,"..","src");

var projectFolder = Path.Combine(root, "..", "src", projectName);

var UnitTests = Path.Combine(root, "..", "src", $"{projectName}.Tests");

var artifactsFolder = CreateDirectory(root, "Artifacts");
var gitHubArtifactsFolder = CreateDirectory(artifactsFolder, "GitHub");
var nuGetArtifactsFolder = CreateDirectory(artifactsFolder, "NuGet");