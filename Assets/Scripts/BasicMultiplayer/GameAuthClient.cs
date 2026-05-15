using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BasicMultiplayer
{
    public sealed class GameAuthClient : MonoBehaviour
    {
        private const string InstallIdKey = "augmego.auth.install_id";
        private const string RefreshTokenKey = "augmego.auth.refresh_token";
        private const float SaveButtonSize = 72f;
        private const float TopButtonMargin = 12f;
        private const float TopButtonGap = 10f;
        private const float AvButtonReservedSize = 72f;
        private const float AuthDebugResetButtonWidth = 96f;
        private const float AuthDebugResetButtonHeight = 44f;

        [SerializeField] private UdpGameClient udpClient;
        [SerializeField] private bool showSaveProgressPrompt = true;
        [SerializeField] private bool showAuthDebugReset = true;
        [SerializeField] private bool useHttps = true;
        [SerializeField] private int directHttpPort = 8080;
        [SerializeField] private string httpBaseUrlOverride;
        [SerializeField] private string googleServerClientId;

        private string _status = "Signing in";
        private bool _isReady;
        private bool _isSigningIn;
        private bool _isGuest = true;
        private string _accountId = string.Empty;
        private string _displayName = "Guest";
        private string _accessToken = string.Empty;
        private string _refreshToken = string.Empty;
        private string _gameSessionId = string.Empty;

        public bool IsReady => _isReady;
        public bool IsGuest => _isGuest;
        public string AccountId => _accountId;
        public string DisplayName => _displayName;
        public string AccessToken => _accessToken;
        public string GameSessionId => _gameSessionId;
        public bool HasGameSession => !string.IsNullOrWhiteSpace(_gameSessionId);
        public string Status => _status;

        private void Awake()
        {
            if (udpClient == null)
            {
                udpClient = GetComponent<UdpGameClient>();
            }
        }

        private void Start()
        {
            if (udpClient == null)
            {
                udpClient = GetComponent<UdpGameClient>();
            }

            StartCoroutine(SignInOnStartCoroutine());
        }

        private void OnGUI()
        {
            var showSaveButton = showSaveProgressPrompt && _isReady && _isGuest;
            var showResetButton = ShouldShowAuthDebugReset();

            if (!showSaveButton && !showResetButton)
            {
                return;
            }

            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            var uiScale = GetUiScale();
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));
            GUI.color = new Color(1f, 1f, 1f, 0.86f);

            if (showSaveButton && GUI.Button(GetSaveButtonRect(uiScale), "Save"))
            {
                StartSaveProgressFlow();
            }

            if (showResetButton && GUI.Button(GetAuthDebugResetButtonRect(uiScale), "Reset"))
            {
                ResetLocalAuthForDebug();
            }

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        public void StartSaveProgressFlow()
        {
            if (_isSigningIn)
            {
                return;
            }

#if UNITY_IOS && !UNITY_EDITOR
            _isSigningIn = true;
            _status = "Apple sign-in starting";
            AppleSignInBridge.SignIn(
                credential => StartCoroutine(SignInWithAppleCoroutine(credential)),
                error =>
                {
                    _isSigningIn = false;
                    _status = $"Apple sign-in failed: {error}";
                    Debug.LogWarning(_status);
                });
#elif UNITY_ANDROID && !UNITY_EDITOR
            var resolvedGoogleClientId = GetGoogleServerClientId();

            if (string.IsNullOrWhiteSpace(resolvedGoogleClientId))
            {
                _status = "Google sign-in missing Web client ID";
                Debug.LogWarning(_status);
                return;
            }

            _isSigningIn = true;
            _status = "Google sign-in starting";
            GoogleSignInBridge.SignIn(
                resolvedGoogleClientId,
                credential => StartCoroutine(SignInWithGoogleCoroutine(credential)),
                error =>
                {
                    _isSigningIn = false;
                    _status = $"Google sign-in failed: {error}";
                    Debug.LogWarning(_status);
                });
#else
            _status = "Apple/Google sign-in is available on device builds first";
            Debug.Log(_status);
#endif
        }

        private IEnumerator SignInOnStartCoroutine()
        {
            var savedRefreshToken = PlayerPrefs.GetString(RefreshTokenKey, string.Empty);

            if (!string.IsNullOrWhiteSpace(savedRefreshToken))
            {
                yield return RefreshCoroutine(savedRefreshToken);

                if (_isReady)
                {
                    yield break;
                }
            }

            yield return SignInGuestCoroutine();
        }

        private IEnumerator SignInGuestCoroutine()
        {
            _isSigningIn = true;
            _status = "Signing in as guest";

            var request = new GuestAuthRequest
            {
                installId = GetOrCreateInstallId(),
                platform = GetPlatformName(),
                displayName = "Guest"
            };

            yield return PostAuthCoroutine("/auth/guest", request, bearerToken: null);
            _isSigningIn = false;
        }

        private IEnumerator RefreshCoroutine(string refreshToken)
        {
            _isSigningIn = true;
            _status = "Refreshing sign-in";

            var request = new RefreshAuthRequest
            {
                refreshToken = refreshToken,
                platform = GetPlatformName()
            };

            yield return PostAuthCoroutine("/auth/refresh", request, bearerToken: null);
            _isSigningIn = false;
        }

        private IEnumerator SignInWithAppleCoroutine(AppleSignInBridge.Credential credential)
        {
            _isSigningIn = true;
            var appleEmail = FirstNonEmpty(credential.Email, TryGetEmailFromAppleIdToken(credential.IdToken));
            var appleDisplayName = FirstNonEmpty(credential.FullName, appleEmail);
            var debugLabel = FirstNonEmpty(credential.FullName, appleEmail, credential.UserId, "Apple user");
            _status = $"Saving progress for {debugLabel}";
            Debug.Log($"Apple credential received for: {debugLabel}");

            var request = new AppleAuthRequest
            {
                idToken = credential.IdToken,
                platform = GetPlatformName(),
                displayName = appleDisplayName
            };

            yield return PostAuthCoroutine("/auth/apple", request, _accessToken);
            _isSigningIn = false;
        }

        private IEnumerator SignInWithGoogleCoroutine(GoogleSignInBridge.Credential credential)
        {
            _isSigningIn = true;
            var googleDisplayName = FirstNonEmpty(credential.DisplayName, credential.Email);
            var debugLabel = FirstNonEmpty(credential.DisplayName, credential.Email, credential.UserId, "Google user");
            _status = $"Saving progress for {debugLabel}";
            Debug.Log($"Google credential received for: {debugLabel}");

            var request = new GoogleAuthRequest
            {
                idToken = credential.IdToken,
                platform = GetPlatformName(),
                displayName = googleDisplayName
            };

            yield return PostAuthCoroutine("/auth/google", request, _accessToken);
            _isSigningIn = false;
        }

        private void ResetLocalAuthForDebug()
        {
            if (_isSigningIn)
            {
                return;
            }

            StopAllCoroutines();
            udpClient?.Disconnect();
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(InstallIdKey);
            PlayerPrefs.Save();

            _isReady = false;
            _isSigningIn = false;
            _isGuest = true;
            _accountId = string.Empty;
            _displayName = "Guest";
            _accessToken = string.Empty;
            _refreshToken = string.Empty;
            _gameSessionId = string.Empty;
            _status = "Local auth reset";
            Debug.Log("Auth local reset: cleared saved refresh token and install id.");

            StartCoroutine(ResetLocalAuthCoroutine());
        }

        private IEnumerator ResetLocalAuthCoroutine()
        {
            yield return SignInGuestCoroutine();

            if (_isReady && udpClient != null)
            {
                udpClient.Connect();
            }
        }

        private IEnumerator PostAuthCoroutine(string path, object requestBody, string bearerToken)
        {
            var url = BuildHttpUrl(path);
            var json = JsonUtility.ToJson(requestBody);
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10
            };
            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            }

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                _status = $"Auth failed: {request.error}";
                yield break;
            }

            var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);

            if (response == null || string.IsNullOrWhiteSpace(response.gameSessionId))
            {
                _status = "Auth failed: empty response";
                yield break;
            }

            ApplyAuthResponse(response);
        }

        private void ApplyAuthResponse(AuthResponse response)
        {
            _accountId = response.accountId;
            _displayName = string.IsNullOrWhiteSpace(response.displayName) ? "Player" : response.displayName;
            _isGuest = response.isGuest;
            _accessToken = response.accessToken;
            _refreshToken = response.refreshToken;
            _gameSessionId = response.gameSessionId;
            _isReady = true;
            _status = _isGuest ? "Guest account ready" : "Account saved";
            Debug.Log($"Auth session ready: {_displayName} ({(_isGuest ? "guest" : "saved")}) account {_accountId}");

            if (!string.IsNullOrWhiteSpace(_refreshToken))
            {
                PlayerPrefs.SetString(RefreshTokenKey, _refreshToken);
                PlayerPrefs.Save();
            }
        }

        private string BuildHttpUrl(string path)
        {
            if (!string.IsNullOrWhiteSpace(httpBaseUrlOverride))
            {
                return $"{httpBaseUrlOverride.TrimEnd('/')}{path}";
            }

            var host = udpClient != null ? udpClient.ServerHost : string.Empty;

            if (string.IsNullOrWhiteSpace(host))
            {
                host = "localhost";
            }

            return useHttps
                ? $"https://{host}{path}"
                : $"http://{host}:{directHttpPort}{path}";
        }

        private string GetGoogleServerClientId()
        {
            var configuredClientId = FirstNonEmpty(
                googleServerClientId,
                LoadAuthConfig()?.googleServerClientId);

            return IsPlaceholderClientId(configuredClientId) ? string.Empty : configuredClientId;
        }

        private static bool IsPlaceholderClientId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                || value.Contains("REPLACE", StringComparison.OrdinalIgnoreCase)
                || value.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
        }

        private static AuthConfig LoadAuthConfig()
        {
            var configAsset = Resources.Load<TextAsset>("AugmegoAuthConfig");

            if (configAsset == null || string.IsNullOrWhiteSpace(configAsset.text))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<AuthConfig>(configAsset.text);
            }
            catch (ArgumentException)
            {
                Debug.LogWarning("Auth config could not be parsed from Resources/AugmegoAuthConfig.json.");
                return null;
            }
        }

        private static string GetOrCreateInstallId()
        {
            var installId = PlayerPrefs.GetString(InstallIdKey, string.Empty);

            if (!string.IsNullOrWhiteSpace(installId))
            {
                return installId;
            }

            installId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(InstallIdKey, installId);
            PlayerPrefs.Save();
            return installId;
        }

        private static string GetPlatformName()
        {
#if UNITY_IOS
            return "ios";
#elif UNITY_ANDROID
            return "android";
#elif UNITY_STANDALONE_OSX
            return "macos";
#elif UNITY_STANDALONE_WIN
            return "windows";
#elif UNITY_STANDALONE_LINUX
            return "linux";
#else
            return Application.platform.ToString();
#endif
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string TryGetEmailFromAppleIdToken(string idToken)
        {
            try
            {
                var parts = idToken.Split('.');

                if (parts.Length != 3)
                {
                    return string.Empty;
                }

                var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                var payload = JsonUtility.FromJson<AppleIdTokenPayload>(payloadJson);
                return payload?.email ?? string.Empty;
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentException)
            {
                return string.Empty;
            }
        }

        private static byte[] Base64UrlDecode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');

            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            return Convert.FromBase64String(padded);
        }

        private static float GetUiScale()
        {
#if UNITY_IOS || UNITY_ANDROID
            return Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 430f, 1.35f, 2.1f);
#else
            return 1f;
#endif
        }

        private static Rect GetSaveButtonRect(float uiScale)
        {
            var safeArea = Screen.safeArea;
            var leftInset = safeArea.xMin / uiScale;
            var topInset = (Screen.height - safeArea.yMax) / uiScale;
            var left = Mathf.Max(TopButtonMargin, leftInset + TopButtonMargin);
            var top = Mathf.Max(
                TopButtonMargin,
                topInset + TopButtonMargin + AvButtonReservedSize + TopButtonGap);
            return new Rect(
                left,
                top,
                SaveButtonSize,
                SaveButtonSize);
        }

        private static Rect GetAuthDebugResetButtonRect(float uiScale)
        {
            var saveRect = GetSaveButtonRect(uiScale);
            return new Rect(
                saveRect.x,
                saveRect.y,
                AuthDebugResetButtonWidth,
                AuthDebugResetButtonHeight);
        }

        private bool ShouldShowAuthDebugReset()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return showAuthDebugReset && _isReady && !_isSigningIn && !_isGuest;
#else
            return false;
#endif
        }

        [Serializable]
        private sealed class GuestAuthRequest
        {
            public string installId;
            public string platform;
            public string displayName;
        }

        [Serializable]
        private sealed class RefreshAuthRequest
        {
            public string refreshToken;
            public string platform;
        }

        [Serializable]
        private sealed class AppleAuthRequest
        {
            public string idToken;
            public string platform;
            public string displayName;
        }

        [Serializable]
        private sealed class GoogleAuthRequest
        {
            public string idToken;
            public string platform;
            public string displayName;
        }

        [Serializable]
        private sealed class AppleIdTokenPayload
        {
            public string email;
        }

        [Serializable]
        private sealed class AuthConfig
        {
            public string googleServerClientId;
        }

        [Serializable]
        private sealed class AuthResponse
        {
            public string accountId;
            public string displayName;
            public bool isGuest;
            public string accessToken;
            public string refreshToken;
            public string gameSessionId;
            public int expiresInSeconds;
        }
    }
}
