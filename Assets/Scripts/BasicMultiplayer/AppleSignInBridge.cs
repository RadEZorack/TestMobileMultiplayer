using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BasicMultiplayer
{
    public sealed class AppleSignInBridge : MonoBehaviour
    {
        private const string BridgeObjectName = "Augmego Apple Sign-In Bridge";
        private static Action<Credential> _success;
        private static Action<string> _failure;
        private static AppleSignInBridge _instance;

        public static void SignIn(Action<Credential> success, Action<string> failure)
        {
            _success = success;
            _failure = failure;
            EnsureInstance();

#if UNITY_IOS && !UNITY_EDITOR
            AugmegoStartSignInWithApple(BridgeObjectName, nameof(HandleAppleSignInResult));
#else
            _failure?.Invoke("Sign in with Apple is only available in iOS player builds.");
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
            _instance = bridgeObject.GetComponent<AppleSignInBridge>()
                ?? bridgeObject.AddComponent<AppleSignInBridge>();
        }

        private void HandleAppleSignInResult(string json)
        {
            var result = JsonUtility.FromJson<AppleSignInResult>(json);

            if (result != null && !string.IsNullOrWhiteSpace(result.idToken))
            {
                var success = _success;
                ClearCallbacks();
                success?.Invoke(new Credential(
                    result.idToken,
                    result.userId,
                    result.email,
                    result.fullName));
                return;
            }

            var failure = _failure;
            ClearCallbacks();
            failure?.Invoke(result?.error ?? "Apple sign-in did not return an identity token.");
        }

        private static void ClearCallbacks()
        {
            _success = null;
            _failure = null;
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void AugmegoStartSignInWithApple(string gameObjectName, string callbackMethodName);
#endif

        public readonly struct Credential
        {
            public Credential(string idToken, string userId, string email, string fullName)
            {
                IdToken = idToken ?? string.Empty;
                UserId = userId ?? string.Empty;
                Email = email ?? string.Empty;
                FullName = fullName ?? string.Empty;
            }

            public string IdToken { get; }
            public string UserId { get; }
            public string Email { get; }
            public string FullName { get; }
        }

        [Serializable]
        private sealed class AppleSignInResult
        {
            public string idToken;
            public string userId;
            public string email;
            public string fullName;
            public string error;
        }
    }
}
