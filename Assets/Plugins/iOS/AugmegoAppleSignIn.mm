#import <AuthenticationServices/AuthenticationServices.h>
#import <UIKit/UIKit.h>

extern "C" void UnitySendMessage(const char *obj, const char *method, const char *msg);
extern UIViewController *UnityGetGLViewController(void);

@interface AugmegoAppleSignInDelegate : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@property(nonatomic, copy) NSString *gameObjectName;
@property(nonatomic, copy) NSString *callbackMethodName;
- (void)start;
@end

static AugmegoAppleSignInDelegate *s_appleSignInDelegate = nil;

static void AugmegoSendAppleSignInResult(NSString *gameObjectName, NSString *callbackMethodName, NSDictionary *payload)
{
    NSError *error = nil;
    NSData *jsonData = [NSJSONSerialization dataWithJSONObject:payload options:0 error:&error];
    NSString *json = error == nil && jsonData != nil
        ? [[NSString alloc] initWithData:jsonData encoding:NSUTF8StringEncoding]
        : @"{\"error\":\"Failed to encode Apple sign-in result.\"}";

    UnitySendMessage(gameObjectName.UTF8String, callbackMethodName.UTF8String, json.UTF8String);
}

@implementation AugmegoAppleSignInDelegate

- (void)start
{
    if (@available(iOS 13.0, *)) {
        ASAuthorizationAppleIDProvider *provider = [[ASAuthorizationAppleIDProvider alloc] init];
        ASAuthorizationAppleIDRequest *request = [provider createRequest];
        request.requestedScopes = @[ASAuthorizationScopeFullName, ASAuthorizationScopeEmail];

        ASAuthorizationController *controller = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
        controller.delegate = self;
        controller.presentationContextProvider = self;
        [controller performRequests];
    } else {
        AugmegoSendAppleSignInResult(
            self.gameObjectName,
            self.callbackMethodName,
            @{@"error": @"Sign in with Apple requires iOS 13 or newer."});
    }
}

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController *)controller API_AVAILABLE(ios(13.0))
{
    UIViewController *viewController = UnityGetGLViewController();
    return viewController.view.window;
}

- (void)authorizationController:(ASAuthorizationController *)controller didCompleteWithAuthorization:(ASAuthorization *)authorization API_AVAILABLE(ios(13.0))
{
    ASAuthorizationAppleIDCredential *credential = authorization.credential;

    if (![credential isKindOfClass:[ASAuthorizationAppleIDCredential class]] || credential.identityToken == nil) {
        AugmegoSendAppleSignInResult(
            self.gameObjectName,
            self.callbackMethodName,
            @{@"error": @"Apple sign-in did not return an identity token."});
        return;
    }

    NSString *idToken = [[NSString alloc] initWithData:credential.identityToken encoding:NSUTF8StringEncoding];
    NSString *email = credential.email ?: @"";
    NSString *fullName = @"";

    if (credential.fullName != nil) {
        NSPersonNameComponentsFormatter *formatter = [[NSPersonNameComponentsFormatter alloc] init];
        fullName = [formatter stringFromPersonNameComponents:credential.fullName] ?: @"";
    }

    AugmegoSendAppleSignInResult(
        self.gameObjectName,
        self.callbackMethodName,
        @{
            @"idToken": idToken ?: @"",
            @"userId": credential.user ?: @"",
            @"email": email,
            @"fullName": fullName
        });
}

- (void)authorizationController:(ASAuthorizationController *)controller didCompleteWithError:(NSError *)error API_AVAILABLE(ios(13.0))
{
    AugmegoSendAppleSignInResult(
        self.gameObjectName,
        self.callbackMethodName,
        @{@"error": error.localizedDescription ?: @"Apple sign-in failed."});
}

@end

extern "C" void AugmegoStartSignInWithApple(const char *gameObjectName, const char *callbackMethodName)
{
    s_appleSignInDelegate = [[AugmegoAppleSignInDelegate alloc] init];
    s_appleSignInDelegate.gameObjectName = [NSString stringWithUTF8String:gameObjectName ?: ""];
    s_appleSignInDelegate.callbackMethodName = [NSString stringWithUTF8String:callbackMethodName ?: ""];
    [s_appleSignInDelegate start];
}
