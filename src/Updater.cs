using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WindowsIsland
{
    enum UpdateResult { Failed, UpToDate, Available }

    sealed class UpdateInfo
    {
        public string Version;
        public string Sha256;
    }

    // Update trust comes only from the RSA signature checked against the key built into this exe.
    // HTTPS and GitHub are transport: a tampered, unsigned or older release is rejected.
    static class Updater
    {
#if UPDATE_TEST
        // A local test server, used only by builds made with -p:DefineConstants=UPDATE_TEST. Never publish one.
        const string BaseUrl = "http://127.0.0.1:8766/";
#else
        const string BaseUrl = "https://github.com/leotrvs/windows-island/releases/latest/download/";
#endif
        const string ExeName = Installer.Id + ".exe";
        const int MaxManifestBytes = 16 * 1024;
        const int MaxSignatureBytes = 4 * 1024;
        const int MaxExeBytes = 150 * 1024 * 1024;

        static readonly HttpClient Http = CreateClient();

        static string ExePath
        {
            get { return Environment.ProcessPath; }
        }

        static string NewFile
        {
            get { return ExePath + ".new"; }
        }

        static string OldFile
        {
            get { return ExePath + ".old"; }
        }

        // Only the installed copy updates itself, and only when a public key was built in.
        public static bool CanUpdate
        {
#if UPDATE_TEST
            get { return UpdateKey.PublicKeyXml.Length > 0; }
#else
            get { return Installer.IsInstalledCopy && UpdateKey.PublicKeyXml.Length > 0; }
#endif
        }

        static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Installer.Id + "-updater");
            return client;
        }

        // Removes what an earlier update left behind: the previous exe, which was still running at that
        // moment, and the libraries that older versions unpacked to the temp folder.
        public static void CleanUp()
        {
            try
            {
                if (File.Exists(OldFile)) File.Delete(OldFile);
                if (File.Exists(NewFile)) File.Delete(NewFile);
            }
            catch (Exception)
            {
                // Retried on the next start.
            }

            try
            {
                DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
                DirectoryInfo parent = current.Parent;
                if (parent == null || parent.Name != Installer.Id || parent.Parent?.Name != ".net") return;
                foreach (DirectoryInfo old in parent.GetDirectories())
                {
                    if (string.Equals(old.FullName, current.FullName, StringComparison.OrdinalIgnoreCase)) continue;
                    try { old.Delete(true); }
                    catch (Exception) { } // Still in use by another copy
                }
            }
            catch (Exception)
            {
            }
        }

        public static async Task<Tuple<UpdateResult, UpdateInfo>> CheckAsync()
        {
            if (!CanUpdate) return Tuple.Create(UpdateResult.Failed, (UpdateInfo)null);
            try
            {
                byte[] manifestBytes = await DownloadAsync("update.json", MaxManifestBytes);
                byte[] signature = await DownloadAsync("update.json.sig", MaxSignatureBytes);
                if (!HasValidSignature(manifestBytes, signature)) return Tuple.Create(UpdateResult.Failed, (UpdateInfo)null);

                using (JsonDocument manifest = JsonDocument.Parse(Encoding.UTF8.GetString(manifestBytes)))
                {
                    // The signing key is shared with Claude Usage Taskbar, so a valid signature alone is not
                    // enough: the manifest must name this app.
                    JsonElement app;
                    if (!manifest.RootElement.TryGetProperty("app", out app) || app.GetString() != Installer.Id)
                        return Tuple.Create(UpdateResult.Failed, (UpdateInfo)null);

                    string version = manifest.RootElement.GetProperty("version").GetString();
                    string sha256 = manifest.RootElement.GetProperty("sha256").GetString();
                    if (new Version(version) <= new Version(Installer.Version))
                        return Tuple.Create(UpdateResult.UpToDate, (UpdateInfo)null);
                    return Tuple.Create(UpdateResult.Available, new UpdateInfo { Version = version, Sha256 = sha256 });
                }
            }
            catch (Exception)
            {
                return Tuple.Create(UpdateResult.Failed, (UpdateInfo)null); // Tried again on the next check.
            }
        }

        // Returns true when an exe whose hash matches the signed manifest is ready to install.
        public static async Task<bool> DownloadVerifiedAsync(UpdateInfo update)
        {
            try
            {
                byte[] exe = await DownloadAsync(ExeName, MaxExeBytes);
                if (!Convert.ToHexString(SHA256.HashData(exe)).Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
                    return false;
                await File.WriteAllBytesAsync(NewFile, exe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Windows allows renaming a running exe, which is what lets it replace itself.
        public static bool ApplyAndRestart()
        {
            string exe = ExePath;
            try
            {
                if (File.Exists(OldFile)) File.Delete(OldFile);
                File.Move(exe, OldFile);
                File.Move(NewFile, exe);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--after-update") { UseShellExecute = false })?.Dispose();
                return true;
            }
            catch (Exception)
            {
                if (!File.Exists(exe) && File.Exists(OldFile)) File.Move(OldFile, exe);
                return false;
            }
        }

        static bool HasValidSignature(byte[] data, byte[] signature)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.FromXmlString(UpdateKey.PublicKeyXml);
                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        static async Task<byte[]> DownloadAsync(string fileName, int maxBytes)
        {
            using (HttpResponseMessage response = await Http.GetAsync(BaseUrl + fileName, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
#if !UPDATE_TEST
                if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException();
#endif
                if (response.Content.Headers.ContentLength > maxBytes) throw new InvalidDataException();

                using (Stream stream = await response.Content.ReadAsStreamAsync())
                using (MemoryStream buffer = new MemoryStream())
                {
                    byte[] chunk = new byte[81920];
                    int read;
                    while ((read = await stream.ReadAsync(chunk, 0, chunk.Length)) > 0)
                    {
                        buffer.Write(chunk, 0, read);
                        if (buffer.Length > maxBytes) throw new InvalidDataException();
                    }
                    return buffer.ToArray();
                }
            }
        }
    }
}
