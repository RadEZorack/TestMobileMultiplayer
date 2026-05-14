using System.Runtime.InteropServices;

namespace BasicMultiplayer
{
    internal static class IosWebRtcAudioSession
    {
        public static void Configure()
        {
#if UNITY_IOS && !UNITY_EDITOR
            AugmegoConfigureWebRtcAudioSession();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void AugmegoConfigureWebRtcAudioSession();
#endif
    }
}
