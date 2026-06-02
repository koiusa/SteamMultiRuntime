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

        var source = FindSteamNativePath(projectRoot);
        if (string.IsNullOrEmpty(source))
        {
            Debug.LogError("[SteamBuildPostprocess] Steam native library not found. Checked Assets/Packages/PackageCache paths.");
            return;
        }

        var sourceName = Path.GetFileName(source);
        var destinations = new[]
        {
            Path.Combine(appPath, "Contents", "PlugIns", sourceName),
            Path.Combine(appPath, "Contents", "Frameworks", sourceName),
            Path.Combine(appPath, "Contents", "MacOS", sourceName)
        };

        foreach (var destination in destinations)
        {
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            CopyPath(source, destination);
            Debug.Log($"[SteamBuildPostprocess] Copied Steam native library to: {destination}");
        }
    }

    private static string FindSteamNativePath(string projectRoot)
    {
        // Preferred: embedded package under Assets.
        var embeddedRoot = Path.Combine(
            projectRoot,
            "Assets",
            "SteamMultiRuntime",
            "Runtime",
            "Packages",
            "Thirdparty",
            "com.community.netcode.transport.facepunch",
            "Runtime",
            "Facepunch",
            "redistributable_bin",
            "osx");

        var directEmbedded = FindSteamNativeInRoot(embeddedRoot);
        if (!string.IsNullOrEmpty(directEmbedded))
        {
            return directEmbedded;
        }

        // Embedded package under Packages/ (non-Assets installation).
        var embeddedPackageRoot = Path.Combine(
            projectRoot,
            "Packages",
            "com.koiusa.steammultiruntime");

        var embeddedFromPackages = FindSteamNativeInSteamMultiRuntimeRoot(embeddedPackageRoot);
        if (!string.IsNullOrEmpty(embeddedFromPackages))
        {
            return embeddedFromPackages;
        }

        // Next: UPM package under Packages.
        var packageRoot = Path.Combine(
            projectRoot,
            "Packages",
            "com.community.netcode.transport.facepunch",
            "Runtime",
            "Facepunch",
            "redistributable_bin",
            "osx");

        var directPackage = FindSteamNativeInRoot(packageRoot);
        if (!string.IsNullOrEmpty(directPackage))
        {
            return directPackage;
        }

        // Fallback: PackageCache.
        var packageCache = Path.Combine(projectRoot, "Library", "PackageCache");
        if (!Directory.Exists(packageCache))
        {
            return string.Empty;
        }

        var dirs = Directory.GetDirectories(packageCache, "com.koiusa.steammultiruntime@*", SearchOption.TopDirectoryOnly);
        foreach (var dir in dirs)
        {
            var candidate = FindSteamNativeInSteamMultiRuntimeRoot(dir);
            if (!string.IsNullOrEmpty(candidate))
            {
                return candidate;
            }
        }

        // Additional fallback for git/hash naming variants in PackageCache.
        var broadDirs = Directory.GetDirectories(packageCache, "com.koiusa.steammultiruntime*", SearchOption.TopDirectoryOnly);
        foreach (var dir in broadDirs)
        {
            var candidate = FindSteamNativeInSteamMultiRuntimeRoot(dir);
            if (!string.IsNullOrEmpty(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static string FindSteamNativeInSteamMultiRuntimeRoot(string steamMultiRuntimeRoot)
    {
        if (string.IsNullOrWhiteSpace(steamMultiRuntimeRoot) || !Directory.Exists(steamMultiRuntimeRoot))
        {
            return string.Empty;
        }

        // Current package layout.
        var thirdpartyRoot = Path.Combine(
            steamMultiRuntimeRoot,
            "Runtime",
            "Packages",
            "Thirdparty",
            "com.community.netcode.transport.facepunch",
            "Runtime",
            "Facepunch",
            "redistributable_bin",
            "osx");

        var fromThirdparty = FindSteamNativeInRoot(thirdpartyRoot);
        if (!string.IsNullOrEmpty(fromThirdparty))
        {
            return fromThirdparty;
        }

        // Backward-compatible layout without Thirdparty/.
        var legacyRoot = Path.Combine(
            steamMultiRuntimeRoot,
            "Runtime",
            "Packages",
            "com.community.netcode.transport.facepunch",
            "Runtime",
            "Facepunch",
            "redistributable_bin",
            "osx");

        return FindSteamNativeInRoot(legacyRoot);
    }

    private static string FindSteamNativeInRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return string.Empty;
        }

        var bundle = Path.Combine(root, "libsteam_api.bundle");
        if (Directory.Exists(bundle) || File.Exists(bundle))
        {
            return bundle;
        }

        var dylib = Path.Combine(root, "libsteam_api.dylib");
        if (File.Exists(dylib))
        {
            return dylib;
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
