using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Clean;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Frosting;
using Cake.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

/// <summary>One mod produced by this repo: a csproj plus the resources folder it ships.</summary>
public sealed class ModProject
{
    public ModProject(ICakeContext context, string projectName, string resourcesDir)
    {
        ProjectName = projectName;
        ResourcesDir = resourcesDir;

        var modInfo = context.DeserializeJsonFromFile<ModInfo>($"../{resourcesDir}/modinfo.json");
        Version = modInfo.Version;
        Name = modInfo.ModID;
    }

    public string ProjectName { get; }
    public string ResourcesDir { get; }
    public string Version { get; }
    public string Name { get; }

    public string ProjectPath => $"../{ProjectName}/{ProjectName}.csproj";
}

public class BuildContext : FrostingContext
{
    public string BuildConfiguration { get; set; }
    public bool SkipJsonValidation { get; set; }

    /// <summary>
    /// Packed in order, so EffectLib is built before the mod that depends on it.
    /// </summary>
    public IReadOnlyList<ModProject> Mods { get; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        BuildConfiguration = context.Argument("configuration", "Release");
        SkipJsonValidation = context.Argument("skipJsonValidation", false);

        Mods =
        [
            new ModProject(context, "EffectLib", "resources-effectlib"),
            new ModProject(context, "Alchemy", "resources"),
        ];
    }
}

[TaskName("ValidateJson")]
public sealed class ValidateJsonTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        if (context.SkipJsonValidation)
        {
            return;
        }

        foreach (var mod in context.Mods)
        {
            var jsonFiles = context.GetFiles($"../{mod.ResourcesDir}/**/*.json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    var json = File.ReadAllText(file.FullPath);
                    JToken.Parse(json);
                }
                catch (JsonException ex)
                {
                    throw new Exception($"Validation failed for JSON file: {file.FullPath}{Environment.NewLine}{ex.Message}", ex);
                }
            }
        }
    }
}

[TaskName("Build")]
[IsDependentOn(typeof(ValidateJsonTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        foreach (var mod in context.Mods)
        {
            context.DotNetClean(mod.ProjectPath,
                new DotNetCleanSettings
                {
                    Configuration = context.BuildConfiguration
                });
        }

        foreach (var mod in context.Mods)
        {
            context.DotNetPublish(mod.ProjectPath,
                new DotNetPublishSettings
                {
                    Configuration = context.BuildConfiguration
                });
        }
    }
}

[TaskName("Package")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackageTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.EnsureDirectoryExists("../Releases");
        context.CleanDirectory("../Releases");

        foreach (var mod in context.Mods)
        {
            context.EnsureDirectoryExists($"../Releases/{mod.Name}");
            context.CopyFiles($"../{mod.ProjectName}/bin/{context.BuildConfiguration}/Mods/mod/publish/*", $"../Releases/{mod.Name}");
            context.CopyDirectory($"../{mod.ResourcesDir}", $"../Releases/{mod.Name}/");
            context.Zip($"../Releases/{mod.Name}", $"../Releases/{mod.Name}_{mod.Version}.zip");
        }
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(PackageTask))]
public class DefaultTask : FrostingTask
{
}
