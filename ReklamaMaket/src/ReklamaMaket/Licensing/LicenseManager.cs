using System;
using Microsoft.Win32;

namespace ReklamaMaket.Licensing
{
    /// <summary>Holat: kalit yo'q, noto'g'ri, boshqa kompyuter uchun, muddati tugagan yoki faol.</summary>
    public sealed class LicenseState
    {
        public bool IsValid { get; set; }
        public LicenseInfo Info { get; set; }
        public string Message { get; set; }
    }

    /// <summary>Plagin ichida kalitni tekshirish: imzo, kompyuter kodi va muddat.</summary>
    public static class LicenseManager
    {
        /// <summary>Kalit sotib olinadigan sayt.</summary>
        public const string SiteUrl = "https://sunnatilla-designer.uz/";

        /// <summary>
        /// Faollashtirish yoqilganmi. Hozircha o'chirilgan — plagin kalitsiz ishlaydi.
        /// Qayta yoqish: true qiling, keygen.bat → "1) Birinchi sozlash", keyin package.bat.
        /// </summary>
        public static readonly bool Enabled = false;

        private static string machineCode;

        /// <summary>Shu kompyuterning kodi (Windows MachineGuid asosida).</summary>
        public static string MachineCode => machineCode ?? (machineCode = LicenseFormat.MachineCodeFor(MachineId()));

        public static bool IsConfigured => LicensePublicKey.X.Length > 0 && LicensePublicKey.Y.Length > 0;

        public static LicenseState Check(string key)
        {
            if (!IsConfigured)
                return new LicenseState { Message = "Kalit tizimi sozlanmagan (sotuvchi keygen.bat → «Birinchi sozlash» qilishi kerak)." };
            if (string.IsNullOrWhiteSpace(key))
                return new LicenseState { Message = "Litsenziya kalitini kiriting." };

            LicenseInfo info;
            string error;
            using (var publicKey = LicenseFormat.PublicKey(LicensePublicKey.X, LicensePublicKey.Y))
            {
                if (!LicenseFormat.TryRead(key, publicKey, out info, out error))
                    return new LicenseState { Message = error };
            }

            if (!string.Equals(info.MachineCode, MachineCode, StringComparison.OrdinalIgnoreCase))
                return new LicenseState { Info = info, Message = "Bu kalit boshqa kompyuter uchun berilgan. Shu kompyuter kodini sotuvchiga yuboring." };
            if (info.Expires.HasValue && DateTime.UtcNow.Date > info.Expires.Value.Date)
                return new LicenseState { Info = info, Message = "Kalit muddati " + info.Expires.Value.ToString("dd.MM.yyyy") + " da tugagan." };

            return new LicenseState { IsValid = true, Info = info, Message = Describe(info) };
        }

        public static string Describe(LicenseInfo info)
        {
            string owner = string.IsNullOrWhiteSpace(info.Name) ? "Litsenziya faol" : info.Name;
            return owner + " · " + (info.Expires.HasValue ? info.Expires.Value.ToString("dd.MM.yyyy") + " gacha" : "muddatsiz");
        }

        private static string MachineId()
        {
            try
            {
                using (RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                {
                    if (key?.GetValue("MachineGuid") is string guid && guid.Length > 0)
                        return guid;
                }
            }
            catch
            {
                // Ruxsat bo'lmasa — kompyuter nomi
            }
            return Environment.MachineName;
        }
    }
}
