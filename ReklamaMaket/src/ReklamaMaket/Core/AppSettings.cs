using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace ReklamaMaket.Core
{
    /// <summary>
    /// Foydalanuvchi sozlamalari: %APPDATA%\ReklamaMaket\settings.json.
    /// API kalit Windows DPAPI bilan shifrlanadi — faqat shu Windows foydalanuvchisi o'qiy oladi.
    /// </summary>
    [DataContract]
    public sealed class AppSettings
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ReklamaMaket.OpenAI");

        [DataMember] public string ApiKeyProtected { get; set; }
        [DataMember] public string Model { get; set; } = "gpt-image-2";
        [DataMember] public string Quality { get; set; } = "medium";
        [DataMember] public string Size { get; set; } = "auto";
        [DataMember] public string Prompt { get; set; }
        [DataMember] public bool Day { get; set; } = true;
        [DataMember] public bool Night { get; set; } = true;

        /// <summary>Dizayn chizilgach ChatGPT'ga avtomatik yuborish.</summary>
        [DataMember] public bool AutoSend { get; set; }

        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReklamaMaket");

        private static string FilePath => Path.Combine(Folder, "settings.json");

        public string ApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(ApiKeyProtected))
                    return string.Empty;
                try
                {
                    byte[] data = ProtectedData.Unprotect(Convert.FromBase64String(ApiKeyProtected), Entropy, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(data);
                }
                catch (Exception ex) when (ex is CryptographicException || ex is FormatException)
                {
                    return string.Empty;
                }
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    ApiKeyProtected = null;
                    return;
                }
                byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), Entropy, DataProtectionScope.CurrentUser);
                ApiKeyProtected = Convert.ToBase64String(data);
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (FileStream stream = File.OpenRead(FilePath))
                        return (AppSettings)new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(stream) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReklamaMaket: sozlamalarni o'qib bo'lmadi: " + ex);
            }
            return new AppSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder);
            using (FileStream stream = File.Create(FilePath))
                new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(stream, this);
        }
    }
}
