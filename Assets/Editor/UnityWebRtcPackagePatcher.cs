#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class UnityWebRtcPackagePatcher
{
    private const string PackageName = "com.unity.webrtc@";
    private const string RelativeWebRtcPath = "Runtime/Scripts/WebRTC.cs";
    private const string OriginalBlock =
        "#if UNITY_IOS\n        internal const string Lib = \"__Internal\";";
    private const string PatchedBlock =
        "#if UNITY_IOS && !UNITY_EDITOR\n        internal const string Lib = \"__Internal\";";

    static UnityWebRtcPackagePatcher()
    {
        EditorApplication.delayCall += PatchAfterEditorStartup;
    }

    [DidReloadScripts]
    private static void PatchAfterScriptReload()
    {
        PatchPackageCache(refreshAssetDatabaseWhenChanged: true);
    }

    private static void PatchAfterEditorStartup()
    {
        PatchPackageCache(refreshAssetDatabaseWhenChanged: true);
    }

    public static bool PatchPackageCache(bool refreshAssetDatabaseWhenChanged)
    {
        var packageCache = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");

        if (!Directory.Exists(packageCache))
        {
            return false;
        }

        var changed = false;

        foreach (var path in Directory.GetFiles(packageCache, "WebRTC.cs", SearchOption.AllDirectories))
        {
            if (!IsUnityWebRtcScript(path))
            {
                continue;
            }

            var text = File.ReadAllText(path);

            if (text.Contains(PatchedBlock, StringComparison.Ordinal))
            {
                continue;
            }

            if (!text.Contains(OriginalBlock, StringComparison.Ordinal))
            {
                Debug.LogWarning($"Unity WebRTC patcher found an unexpected WebRTC.cs shape and skipped: {path}");
                continue;
            }

            File.WriteAllText(path, text.Replace(OriginalBlock, PatchedBlock));
            changed = true;
            Debug.Log($"Patched Unity WebRTC iOS Editor DllImport guard: {path}");
        }

        if (changed && refreshAssetDatabaseWhenChanged)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        return changed;
    }

    [MenuItem("Tools/Patch Unity WebRTC iOS Editor Bug")]
    private static void PatchFromMenu()
    {
        var changed = PatchPackageCache(refreshAssetDatabaseWhenChanged: true);
        Debug.Log(changed
            ? "Unity WebRTC iOS Editor patch applied."
            : "Unity WebRTC iOS Editor patch was already applied or the package was not resolved yet.");
    }

    private static bool IsUnityWebRtcScript(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains($"/{PackageName}", StringComparison.Ordinal)
            && normalized.EndsWith($"/{RelativeWebRtcPath}", StringComparison.Ordinal);
    }
}
#endif
