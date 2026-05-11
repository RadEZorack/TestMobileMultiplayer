#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IosLocalNetworkPostprocessor
{
    [PostProcessBuild]
    public static void AddLocalNetworkPermission(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        plist.root.SetString(
            "NSLocalNetworkUsageDescription",
            "This game connects to a multiplayer server on your local network.");

        plist.WriteToFile(plistPath);
    }
}
#endif
