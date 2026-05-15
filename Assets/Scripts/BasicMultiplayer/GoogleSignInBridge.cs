using System;
using UnityEngine;

namespace BasicMultiplayer
{
    public sealed class GoogleSignInBridge : MonoBehaviour
    {
        private const string BridgeObjectName = "Augmego Google Sign-In Bridge";
        private static Action<Credential> _success;
        private static Action<string> _failure;
        private static GoogleSignInBridge _instance;

        public static void SignIn(string serverClientId, Action<Credential> success, Action<string> failure)
        {
            _success = success;
            _failure = failure;
            EnsureInstance();

#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(serverClientId))
            {
                Fail("Google sign-in is missing the Web client ID.");
                return;
            }

            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var googleSignIn = new AndroidJavaClass("ca.augmego.auth.AugmegoGoogleSignIn");
                googleSignIn.CallStatic(
                    "start",
                    activity,
                    BridgeObjectName,
                    nameof(HandleGoogleSignInResult),
                    serverClientId);
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
            }
#else
            Fail("Sign in with Google is only available in Android player builds.");
#endif
        }

        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
            }

            var existing = GameObject.Find(BridgeObjectName);
            var bridgeObject = existing != null ? existing : new GameObject(BridgeObjectName);
            DontDestroyOnLoad(bridgeObject);
            _instance = bridgeObject.GetComponent<GoogleSignInBridge>()
                ?? bridgeObject.AddComponent<GoogleSignInBridge>();
        }

        private void HandleGoogleSignInResult(string json)
        {
            var result = JsonUtility.FromJson<GoogleSignInResult>(json);

            if (result != null && !string.IsNullOrWhiteSpace(result.idToken))
            {
                var success = _success;
                ClearCallbacks();
                success?.Invoke(new Credential(
                    result.idToken,
                    result.userId,
                    result.email,
                    result.displayName));
                return;
            }

            Fail(result?.error ?? "Google sign-in did not return an identity token.");
        }

        private static void Fail(string error)
        {
            var failure = _failure;
            ClearCallbacks();
            failure?.Invoke(error);
        }

        private static void ClearCallbacks()
        {
            _success = null;
            _failure = null;
        }

        public readonly struct Credential
        {
            public Credential(string idToken, string userId, string email, string displayName)
            {
                IdToken = idToken ?? string.Empty;
                UserId = userId ?? string.Empty;
                Email = email ?? string.Empty;
                DisplayName = displayName ?? string.Empty;
            }

            public string IdToken { get; }
            public string UserId { get; }
            public string Email { get; }
            public string DisplayName { get; }
        }

        [Serializable]
        private sealed class GoogleSignInResult
        {
            public string idToken;
            public string userId;
            public string email;
            public string displayName;
            public string error;
        }
    }
}
