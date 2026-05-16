#if UNITY_EDITOR && UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IosLocalNetworkPostprocessor
{
    private const bool IncludeWebRtcMediaPermissions = false;

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

        if (IncludeWebRtcMediaPermissions)
        {
            plist.root.SetString(
                "NSCameraUsageDescription",
                "This game can show your camera feed above your multiplayer avatar.");
            plist.root.SetString(
                "NSMicrophoneUsageDescription",
                "This game can share your microphone audio with nearby multiplayer peers.");
        }

        plist.WriteToFile(plistPath);

        var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);

        var mainTargetGuid = project.GetUnityMainTargetGuid();
        var frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
        project.SetBuildProperty(mainTargetGuid, "ENABLE_BITCODE", "NO");
        project.SetBuildProperty(frameworkTargetGuid, "ENABLE_BITCODE", "NO");
        project.WriteToFile(projectPath);

        var capabilityManager = new ProjectCapabilityManager(
            projectPath,
            "Unity-iPhone.entitlements",
            targetGuid: mainTargetGuid);
        capabilityManager.AddSignInWithApple();
        capabilityManager.WriteToFile();
    }
}
#endif
