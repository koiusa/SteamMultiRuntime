#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class SteamBuildPostprocess : IPostprocessBuildWithReport
{
    // Replace with your real Steam App ID for production builds.
    private const uint DefaultSteamAppId = 480;

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.StandaloneOSX)
        {
            return;
        }

        var appPath = report.summary.outputPath;
        var appId = ResolveSteamAppId();

        WriteSteamAppIdFile(appPath, appId);
        CopySteamBundleToBuild(appPath);
    }

    private static uint ResolveSteamAppId()
    {
        var env = Environment.GetEnvironmentVariable("STEAM_APP_ID");
        if (!string.IsNullOrWhiteSpace(env) && uint.TryParse(env, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return DefaultSteamAppId;
    }

    private static void WriteSteamAppIdFile(string appPath, uint appId)
    {
        var macOsDir = Path.Combine(appPath, "Contents", "MacOS");
        Directory.CreateDirectory(macOsDir);

        var steamAppIdPath = Path.Combine(macOsDir, "steam_appid.txt");
        File.WriteAllText(steamAppIdPath, appId.ToString() + "\n");
        Debug.Log($"[SteamBuildPostprocess] Wrote steam_appid.txt: {steamAppIdPath} (appid={appId})");
    }

    private static void CopySteamBundleToBuild(string appPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrEmpty(projectRoot))
        {
            Debug.LogError("[SteamBuildPostprocess] Could not resolve project root.");
            return;
        }

        var packageCache = Path.Combine(projectRoot, "Library", "PackageCache");
        if (!Directory.Exists(packageCache))
        {
            Debug.LogError($"[SteamBuildPostprocess] PackageCache not found: {packageCache}");
            return;
        }

        var sourceBundle = FindSteamBundleInPackageCache(packageCache);
        if (string.IsNullOrEmpty(sourceBundle))
        {
            Debug.LogError("[SteamBuildPostprocess] libsteam_api.bundle not found in PackageCache.");
            return;
        }

        var destinations = new[]
        {
            Path.Combine(appPath, "Contents", "PlugIns", "libsteam_api.bundle"),
            Path.Combine(appPath, "Contents", "Frameworks", "libsteam_api.bundle"),
            Path.Combine(appPath, "Contents", "MacOS", "libsteam_api.bundle")
        };

        foreach (var destinationBundle in destinations)
        {
            var destinationDir = Path.GetDirectoryName(destinationBundle);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            CopyPath(sourceBundle, destinationBundle);
            Debug.Log($"[SteamBuildPostprocess] Copied libsteam_api.bundle to: {destinationBundle}");
        }
    }

    private static string FindSteamBundleInPackageCache(string packageCache)
    {
        var dirs = Directory.GetDirectories(packageCache, "com.koiusa.steammultiruntime@*", SearchOption.TopDirectoryOnly);
        foreach (var dir in dirs)
        {
            var candidate = Path.Combine(
                dir,
                "Runtime",
                "Packages",
                "com.community.netcode.transport.facepunch",
                "Runtime",
                "Facepunch",
                "redistributable_bin",
                "osx",
                "libsteam_api.bundle");

            if (Directory.Exists(candidate) || File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static void CopyPath(string sourcePath, string destinationPath)
    {
        if (Directory.Exists(destinationPath))
        {
            Directory.Delete(destinationPath, true);
        }
        else if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        if (Directory.Exists(sourcePath))
        {
            CopyDirectoryRecursive(sourcePath, destinationPath);
            return;
        }

        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destinationPath, true);
            return;
        }

        throw new FileNotFoundException($"Source bundle not found: {sourcePath}");
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destinationFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destinationFile, true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destinationSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destinationSubDir);
        }
    }
}
#endif
