#import <AVFoundation/AVFoundation.h>

extern "C" void AugmegoConfigureWebRtcAudioSession(void)
{
    AVAudioSession *session = [AVAudioSession sharedInstance];
    NSError *error = nil;

    AVAudioSessionCategoryOptions options =
        AVAudioSessionCategoryOptionDefaultToSpeaker |
        AVAudioSessionCategoryOptionAllowBluetoothHFP;

    if (@available(iOS 10.0, *)) {
        options |= AVAudioSessionCategoryOptionAllowBluetoothA2DP;
    }

    [session setCategory:AVAudioSessionCategoryPlayAndRecord
             withOptions:options
                   error:&error];

    error = nil;
    [session setMode:AVAudioSessionModeVideoChat error:&error];

    error = nil;
    [session overrideOutputAudioPort:AVAudioSessionPortOverrideSpeaker error:&error];

    error = nil;
    [session setActive:YES error:&error];
}
