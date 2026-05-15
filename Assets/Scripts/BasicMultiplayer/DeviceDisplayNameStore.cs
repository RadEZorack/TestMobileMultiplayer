using System.Text;
using UnityEngine;

namespace BasicMultiplayer
{
    public static class DeviceDisplayNameStore
    {
        private const string DisplayNameKey = "augmego.device_display_name";
        private const int MaxDisplayNameLength = 16;

        public static string Get()
        {
            return Sanitize(PlayerPrefs.GetString(DisplayNameKey, "Player"));
        }

        public static void Set(string displayName)
        {
            PlayerPrefs.SetString(DisplayNameKey, Sanitize(displayName));
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(DisplayNameKey);
            PlayerPrefs.Save();
        }

        public static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Player";
            }

            var builder = new StringBuilder(MaxDisplayNameLength);
            var previousWasWhitespace = true;

            foreach (var character in value.Trim())
            {
                if (char.IsWhiteSpace(character))
                {
                    if (!previousWasWhitespace && builder.Length < MaxDisplayNameLength)
                    {
                        builder.Append(' ');
                        previousWasWhitespace = true;
                    }

                    continue;
                }

                if (char.IsControl(character))
                {
                    continue;
                }

                builder.Append(character);
                previousWasWhitespace = false;

                if (builder.Length >= MaxDisplayNameLength)
                {
                    break;
                }
            }

            var sanitized = builder.ToString().Trim();
            return sanitized.Length == 0 ? "Player" : sanitized;
        }
    }
}
