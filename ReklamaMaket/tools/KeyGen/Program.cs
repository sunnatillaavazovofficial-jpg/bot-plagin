using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReklamaMaket.Licensing;

namespace ReklamaMaket.KeyGen
{
    /// <summary>
    /// Reklama Maket — litsenziya kaliti generatori (faqat sotuvchi uchun).
    ///
    ///   keygen                       menyu
    ///   keygen init                  birinchi sozlash: kalitlar juftini yaratadi
    ///   keygen issue KOD "Ism" [kun] kalit yaratadi (kun: 0 yoki yo'q — muddatsiz)
    ///   keygen check KALIT           kalitni tekshiradi
    ///
    /// Maxfiy kalit %APPDATA%\ReklamaMaket-KeyGen\maxfiy-kalit.json faylida turadi — uni hech kimga
    /// bermang va zaxira nusxasini saqlab qo'ying. Yo'qolsa, yangi kalit berish uchun plaginni
    /// yangi ochiq kalit bilan qayta yig'ish kerak bo'ladi.
    /// </summary>
    internal static class Program
    {
        private static string Folder
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(root))
                    root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(root))
                    throw new InvalidOperationException("Foydalanuvchi papkasi topilmadi.");
                return Path.Combine(root, "ReklamaMaket-KeyGen");
            }
        }
        private static string PrivateKeyFile => Path.Combine(Folder, "maxfiy-kalit.json");
        private static string IssuedLog => Path.Combine(Folder, "berilgan-kalitlar.csv");

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            try
            {
                if (args.Length == 0)
                    return Menu();

                switch (args[0].ToLowerInvariant())
                {
                    case "init":
                        return Init();
                    case "issue":
                        if (args.Length < 3)
                            return Fail("Foydalanish: keygen issue KOMPYUTER-KODI \"Xaridor ismi\" [kun]");
                        return Issue(args[1], args[2], args.Length > 3 ? args[3] : "0");
                    case "check":
                        if (args.Length < 2)
                            return Fail("Foydalanish: keygen check KALIT");
                        return Check(args[1]);
                    default:
                        return Fail("Noma'lum buyruq: " + args[0]);
                }
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private static int Menu()
        {
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Reklama Maket — kalit generatori");
                Console.WriteLine("  1) Birinchi sozlash (bir marta)");
                Console.WriteLine("  2) Litsenziya kaliti yaratish");
                Console.WriteLine("  3) Kalitni tekshirish");
                Console.WriteLine("  0) Chiqish");
                Console.Write("Tanlang: ");
                string choice = Console.ReadLine()?.Trim();
                try
                {
                    switch (choice)
                    {
                        case "1":
                            Init();
                            break;
                        case "2":
                            Console.Write("Xaridorning kompyuter kodi: ");
                            string code = Console.ReadLine();
                            Console.Write("Xaridor ismi: ");
                            string name = Console.ReadLine();
                            Console.Write("Muddati, kun (0 — muddatsiz): ");
                            string days = Console.ReadLine();
                            Issue(code, name, days);
                            break;
                        case "3":
                            Console.Write("Kalit: ");
                            Check(Console.ReadLine());
                            break;
                        case "0":
                        case null:
                            return 0;
                    }
                }
                catch (Exception ex)
                {
                    Fail(ex.Message);
                }
            }
        }

        // ---------------------------------------------------------------- buyruqlar

        private static int Init()
        {
            if (File.Exists(PrivateKeyFile))
            {
                Console.WriteLine("Kalitlar juft allaqachon yaratilgan: " + PrivateKeyFile);
                Console.WriteLine("Ochiq kalit plagin kodiga yozilmoqda (o'zgarmaydi).");
                WritePublicKeyFile(LoadKey());
                return 0;
            }

            Directory.CreateDirectory(Folder);
            using (ECDsa key = LicenseFormat.CreateKey())
            {
                ECParameters p = key.ExportParameters(true);
                var json = JsonSerializer.Serialize(new KeyFile
                {
                    X = Convert.ToBase64String(p.Q.X),
                    Y = Convert.ToBase64String(p.Q.Y),
                    D = Convert.ToBase64String(p.D),
                    Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PrivateKeyFile, json);
                WritePublicKeyFile(key);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Tayyor. Maxfiy kalit: " + PrivateKeyFile);
            Console.ResetColor();
            Console.WriteLine("MUHIM: shu faylning zaxira nusxasini (fleshka / bulut) saqlang va hech kimga bermang.");
            Console.WriteLine("Endi package.bat bilan plagin paketini yig'ing.");
            return 0;
        }

        private static int Issue(string code, string name, string daysText)
        {
            if (!LicenseFormat.TryParseMachineCode(code, out _))
                return Fail("Kompyuter kodi noto'g'ri. Masalan: A1B2-C3D4-E5F6-0718");
            if (!int.TryParse((daysText ?? "0").Trim(), out int days) || days < 0)
                return Fail("Muddat — butun son (kun), 0 — muddatsiz.");

            var info = new LicenseInfo
            {
                Name = (name ?? string.Empty).Trim(),
                MachineCode = code.Trim().ToUpperInvariant(),
                Issued = DateTime.UtcNow.Date,
                Expires = days > 0 ? DateTime.UtcNow.Date.AddDays(days) : (DateTime?)null
            };

            string licenseKey;
            using (ECDsa key = LoadKey())
                licenseKey = LicenseFormat.Issue(key, info);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(licenseKey);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Xaridor: " + (info.Name.Length > 0 ? info.Name : "—") + " · " +
                              (info.Expires.HasValue ? info.Expires.Value.ToString("dd.MM.yyyy") + " gacha" : "muddatsiz"));
            Console.WriteLine("Shu kalitni to'liq nusxalab xaridorga yuboring.");

            bool newLog = !File.Exists(IssuedLog);
            using (var log = new StreamWriter(IssuedLog, append: true, Encoding.UTF8))
            {
                if (newLog)
                    log.WriteLine("sana;ism;kompyuter_kodi;muddati;kalit");
                log.WriteLine(string.Join(";", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                    info.Name.Replace(";", ","), info.MachineCode,
                    info.Expires.HasValue ? info.Expires.Value.ToString("yyyy-MM-dd") : "muddatsiz", licenseKey));
            }
            return 0;
        }

        private static int Check(string licenseKey)
        {
            using (ECDsa key = LoadKey())
            {
                if (!LicenseFormat.TryRead(licenseKey, key, out LicenseInfo info, out string error))
                    return Fail(error);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Kalit haqiqiy.");
                Console.ResetColor();
                Console.WriteLine("Xaridor: " + info.Name);
                Console.WriteLine("Kompyuter kodi: " + info.MachineCode);
                Console.WriteLine("Berilgan: " + info.Issued.ToString("dd.MM.yyyy"));
                Console.WriteLine("Muddati: " + (info.Expires.HasValue ? info.Expires.Value.ToString("dd.MM.yyyy") + " gacha" : "muddatsiz"));
                return 0;
            }
        }

        // ---------------------------------------------------------------- yordamchilar

        private static ECDsa LoadKey()
        {
            if (!File.Exists(PrivateKeyFile))
                throw new InvalidOperationException("Avval «1) Birinchi sozlash» ni bajaring (maxfiy kalit topilmadi: " + PrivateKeyFile + ").");
            var file = JsonSerializer.Deserialize<KeyFile>(File.ReadAllText(PrivateKeyFile));
            return LicenseFormat.PrivateKey(file.X, file.Y, file.D);
        }

        /// <summary>Ochiq kalitni plagin kodiga yozadi (src/ReklamaMaket/Licensing/LicensePublicKey.cs).</summary>
        private static void WritePublicKeyFile(ECDsa key)
        {
            string path = FindPublicKeyFile();
            if (path == null)
            {
                Console.WriteLine("Diqqat: LicensePublicKey.cs topilmadi — keygen.bat ni ReklamaMaket papkasidan ishga tushiring.");
                return;
            }

            ECParameters p = key.ExportParameters(false);
            string text = File.ReadAllText(path);
            text = ReplaceConst(text, "X", Convert.ToBase64String(p.Q.X));
            text = ReplaceConst(text, "Y", Convert.ToBase64String(p.Q.Y));
            File.WriteAllText(path, text, new UTF8Encoding(false));
            Console.WriteLine("Ochiq kalit yozildi: " + path);
        }

        private static string ReplaceConst(string text, string name, string value)
        {
            string marker = "public const string " + name + " = \"";
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
                throw new InvalidOperationException("LicensePublicKey.cs kutilgan ko'rinishda emas.");
            start += marker.Length;
            int end = text.IndexOf('"', start);
            return text.Substring(0, start) + value + text.Substring(end);
        }

        private static string FindPublicKeyFile()
        {
            string dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 5 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "src", "ReklamaMaket", "Licensing", "LicensePublicKey.cs");
                if (File.Exists(candidate))
                    return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static int Fail(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
            return 1;
        }

        private sealed class KeyFile
        {
            public string X { get; set; }
            public string Y { get; set; }
            public string D { get; set; }
            public string Created { get; set; }
        }
    }
}
