package ca.augmego.auth;

import android.app.Activity;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.os.CancellationSignal;
import android.os.Build;
import android.util.Log;

import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;

import org.json.JSONException;
import org.json.JSONObject;

import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Locale;
import java.util.concurrent.Executor;

public final class AugmegoGoogleSignIn {
    private static final String TAG = "AugmegoGoogleSignIn";

    private AugmegoGoogleSignIn() {
    }

    public static void start(
            Activity activity,
            String gameObjectName,
            String callbackMethodName,
            String serverClientId) {
        if (activity == null) {
            sendError(gameObjectName, callbackMethodName, "Unity activity is unavailable.");
            return;
        }

        if (serverClientId == null || serverClientId.trim().isEmpty()) {
            sendError(gameObjectName, callbackMethodName, "Google Web client ID is missing.");
            return;
        }

        activity.runOnUiThread(() -> startOnUiThread(
                activity,
                gameObjectName,
                callbackMethodName,
                serverClientId.trim()));
    }

    private static void startOnUiThread(
            Activity activity,
            String gameObjectName,
            String callbackMethodName,
            String serverClientId) {
        try {
            logAuthConfiguration(activity, serverClientId);

            CredentialManager credentialManager = CredentialManager.create(activity);
            GetSignInWithGoogleOption googleOption =
                    new GetSignInWithGoogleOption.Builder(serverClientId).build();
            GetCredentialRequest request = new GetCredentialRequest.Builder()
                    .addCredentialOption(googleOption)
                    .build();
            CancellationSignal cancellationSignal = new CancellationSignal();
            Executor executor = activity::runOnUiThread;

            credentialManager.getCredentialAsync(
                    activity,
                    request,
                    cancellationSignal,
                    executor,
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                        @Override
                        public void onResult(GetCredentialResponse result) {
                            handleCredentialResult(gameObjectName, callbackMethodName, result);
                        }

                        @Override
                        public void onError(GetCredentialException error) {
                            Log.w(TAG, "Google sign-in failed.", error);
                            sendError(gameObjectName, callbackMethodName, describeCredentialError(error));
                        }
                    });
        } catch (Exception exception) {
            Log.w(TAG, "Google sign-in could not start.", exception);
            sendError(gameObjectName, callbackMethodName, exception.getMessage());
        }
    }

    private static void logAuthConfiguration(Activity activity, String serverClientId) {
        String packageName = activity.getPackageName();
        Log.i(
                TAG,
                "Starting Google sign-in. package="
                        + packageName
                        + " sha1="
                        + getSigningCertificateSha1(activity, packageName)
                        + " serverClientId="
                        + serverClientId);
    }

    private static String getSigningCertificateSha1(Activity activity, String packageName) {
        try {
            PackageManager packageManager = activity.getPackageManager();
            PackageInfo packageInfo;
            Signature[] signatures;

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                packageInfo = packageManager.getPackageInfo(
                        packageName,
                        PackageManager.GET_SIGNING_CERTIFICATES);
                signatures = packageInfo.signingInfo.hasMultipleSigners()
                        ? packageInfo.signingInfo.getApkContentsSigners()
                        : packageInfo.signingInfo.getSigningCertificateHistory();
            } else {
                packageInfo = packageManager.getPackageInfo(packageName, PackageManager.GET_SIGNATURES);
                signatures = packageInfo.signatures;
            }

            if (signatures == null || signatures.length == 0) {
                return "unknown";
            }

            MessageDigest digest = MessageDigest.getInstance("SHA-1");
            byte[] sha1Bytes = digest.digest(signatures[0].toByteArray());
            StringBuilder builder = new StringBuilder(sha1Bytes.length * 3);

            for (int index = 0; index < sha1Bytes.length; index++) {
                if (index > 0) {
                    builder.append(':');
                }

                builder.append(String.format(Locale.US, "%02X", sha1Bytes[index] & 0xFF));
            }

            return builder.toString();
        } catch (PackageManager.NameNotFoundException | NoSuchAlgorithmException exception) {
            Log.w(TAG, "Could not read signing certificate SHA-1.", exception);
            return "unknown";
        }
    }

    private static String describeCredentialError(GetCredentialException error) {
        String message = error.getMessage();

        if (message == null || message.isEmpty()) {
            message = "Google sign-in failed.";
        }

        if (message.contains("[16]") || message.toLowerCase(Locale.US).contains("reauth")) {
            return message
                    + " Check that Google Cloud has an Android OAuth client with the logged package/SHA-1, "
                    + "and that AugmegoAuthConfig.json uses the Web OAuth client ID.";
        }

        return message;
    }

    private static void handleCredentialResult(
            String gameObjectName,
            String callbackMethodName,
            GetCredentialResponse result) {
        Credential credential = result.getCredential();

        if (!(credential instanceof CustomCredential)) {
            sendError(gameObjectName, callbackMethodName, "Google sign-in returned an unsupported credential.");
            return;
        }

        CustomCredential customCredential = (CustomCredential) credential;

        if (!GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(customCredential.getType())) {
            sendError(gameObjectName, callbackMethodName, "Google sign-in returned an unsupported credential type.");
            return;
        }

        try {
            GoogleIdTokenCredential googleCredential =
                    GoogleIdTokenCredential.createFrom(customCredential.getData());
            String accountId = googleCredential.getId();
            JSONObject payload = new JSONObject();
            payload.put("idToken", nullToEmpty(googleCredential.getIdToken()));
            payload.put("userId", nullToEmpty(accountId));
            payload.put("email", nullToEmpty(accountId));
            payload.put("displayName", nullToEmpty(googleCredential.getDisplayName()));
            send(gameObjectName, callbackMethodName, payload);
        } catch (Exception exception) {
            Log.w(TAG, "Google sign-in token could not be parsed.", exception);
            sendError(gameObjectName, callbackMethodName, exception.getMessage());
        }
    }

    private static void sendError(String gameObjectName, String callbackMethodName, String error) {
        try {
            JSONObject payload = new JSONObject();
            payload.put("error", error == null || error.isEmpty() ? "Google sign-in failed." : error);
            send(gameObjectName, callbackMethodName, payload);
        } catch (JSONException exception) {
            Log.w(TAG, "Google sign-in error payload could not be encoded.", exception);
            UnityPlayer.UnitySendMessage(
                    gameObjectName,
                    callbackMethodName,
                    "{\"error\":\"Google sign-in failed.\"}");
        }
    }

    private static void send(String gameObjectName, String callbackMethodName, JSONObject payload) {
        UnityPlayer.UnitySendMessage(
                gameObjectName,
                callbackMethodName,
                payload.toString());
    }

    private static String nullToEmpty(String value) {
        return value == null ? "" : value;
    }
}
