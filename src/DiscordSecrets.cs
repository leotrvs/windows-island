using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace WindowsIsland
{
    // The Discord app's client secret and the tokens Discord hands out, kept in one file that is encrypted for this Windows
    // user (DPAPI). Any program running as you can still ask Windows to decrypt it, as with any such store. They never go
    // into settings.json, a log or a message.
    sealed class DiscordSecrets
    {
        public string ClientSecret = "";
        public string AccessToken = "";
        public string RefreshToken = "";
        public string ClientId = ""; // The application the tokens belong to: tokens of another one are not used

        static string FilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsIsland", "discord.dat"); }
        }

        public static DiscordSecrets Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new DiscordSecrets();
                byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
                using (JsonDocument document = JsonDocument.Parse(plain))
                {
                    DiscordSecrets secrets = new DiscordSecrets();
                    JsonElement value;
                    if (document.RootElement.TryGetProperty("secret", out value) && value.ValueKind == JsonValueKind.String) secrets.ClientSecret = value.GetString();
                    if (document.RootElement.TryGetProperty("access", out value) && value.ValueKind == JsonValueKind.String) secrets.AccessToken = value.GetString();
                    if (document.RootElement.TryGetProperty("refresh", out value) && value.ValueKind == JsonValueKind.String) secrets.RefreshToken = value.GetString();
                    if (document.RootElement.TryGetProperty("client", out value) && value.ValueKind == JsonValueKind.String) secrets.ClientId = value.GetString();
                    return secrets;
                }
            }
            catch (Exception)
            {
                return new DiscordSecrets(); // Unreadable (another user, a damaged file): start clean
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new { secret = ClientSecret ?? "", access = AccessToken ?? "", refresh = RefreshToken ?? "", client = ClientId ?? "" });
                File.WriteAllBytes(FilePath, ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser));
            }
            catch (Exception)
            {
            }
        }

        // Only the tokens change: what is on disk now is read again, so a secret typed in Settings meanwhile is not lost.
        public static void SaveTokens(string clientId, string access, string refresh)
        {
            DiscordSecrets latest = Load();
            latest.ClientId = clientId ?? "";
            latest.AccessToken = access ?? "";
            latest.RefreshToken = refresh ?? "";
            latest.Save();
        }

        public static bool Exists
        {
            get { return File.Exists(FilePath); }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception) { }
        }
    }
}
