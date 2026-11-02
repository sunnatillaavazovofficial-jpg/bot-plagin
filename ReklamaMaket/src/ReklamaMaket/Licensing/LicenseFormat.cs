using System;
using System.Security.Cryptography;
using System.Text;

namespace ReklamaMaket.Licensing
{
    /// <summary>Litsenziya kaliti ichidagi ma'lumot.</summary>
    public sealed class LicenseInfo
    {
        /// <summary>Xaridor ismi.</summary>
        public string Name { get; set; }

        /// <summary>Kompyuter kodi, masalan "A1B2-C3D4-E5F6-0718".</summary>
        public string MachineCode { get; set; }

        public DateTime Issued { get; set; }

        /// <summary>Oxirgi amal qiladigan kun; null — muddatsiz.</summary>
        public DateTime? Expires { get; set; }
    }

    /// <summary>
    /// Kalit formati: "RM-" + Base32(ma'lumot + ECDSA P-256 imzo).
    /// Imzoni faqat sotuvchidagi maxfiy kalit qo'ya oladi; plagin ichida faqat ochiq kalit bor —
    /// shuning uchun kalitni soxtalashtirib yoki boshqa kompyuterga o'tkazib bo'lmaydi.
    /// Bu fayl plaginga ham, kalit generatoriga (tools/KeyGen) ham kiradi.
    /// </summary>
    public static class LicenseFormat
    {
        public const string Prefix = "RM-";

        private const byte FormatVersion = 1;
        private const int MachineBytes = 8;
        private const int SignatureBytes = 64;
        private const int MaxNameBytes = 48;
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford Base32
        private static readonly DateTime Epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ---------------------------------------------------------------- kompyuter kodi

        /// <summary>Kompyuter identifikatoridan 16 belgili kod (8 bayt SHA-256).</summary>
        public static string MachineCodeFor(string machineId)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("ReklamaMaket|" + (machineId ?? string.Empty).Trim().ToLowerInvariant()));
                return FormatMachineCode(hash, 0);
            }
        }

        private static string FormatMachineCode(byte[] bytes, int offset)
        {
            var text = new StringBuilder();
            for (int i = 0; i < MachineBytes; i++)
            {
                if (i > 0 && i % 2 == 0)
                    text.Append('-');
                text.Append(bytes[offset + i].ToString("X2"));
            }
            return text.ToString();
        }

        /// <summary>"A1B2-C3D4-E5F6-0718" → 8 bayt. Bo'sh joy va chiziqlar e'tiborga olinmaydi.</summary>
        public static bool TryParseMachineCode(string code, out byte[] bytes)
        {
            bytes = null;
            string hex = (code ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).Trim();
            if (hex.Length != MachineBytes * 2)
                return false;

            var result = new byte[MachineBytes];
            for (int i = 0; i < MachineBytes; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out result[i]))
                    return false;
            }
            bytes = result;
            return true;
        }

        // ---------------------------------------------------------------- kalit yaratish (sotuvchida)

        public static string Issue(ECDsa privateKey, LicenseInfo info)
        {
            if (!TryParseMachineCode(info.MachineCode, out byte[] machine))
                throw new ArgumentException("Kompyuter kodi noto'g'ri: 16 ta belgi (0-9, A-F) bo'lishi kerak.");

            byte[] name = Encoding.UTF8.GetBytes((info.Name ?? string.Empty).Trim());
            if (name.Length > MaxNameBytes)
                throw new ArgumentException("Ism juda uzun (48 baytgacha).");

            var data = new byte[1 + MachineBytes + 2 + 2 + 1 + name.Length];
            int p = 0;
            data[p++] = FormatVersion;
            Buffer.BlockCopy(machine, 0, data, p, MachineBytes);
            p += MachineBytes;
            WriteDay(data, ref p, info.Issued);
            WriteDay(data, ref p, info.Expires);
            data[p++] = (byte)name.Length;
            Buffer.BlockCopy(name, 0, data, p, name.Length);

            byte[] signature = privateKey.SignData(data, HashAlgorithmName.SHA256);
            if (signature.Length != SignatureBytes)
                throw new CryptographicException("Kutilmagan imzo uzunligi: " + signature.Length);

            var blob = new byte[data.Length + SignatureBytes];
            Buffer.BlockCopy(data, 0, blob, 0, data.Length);
            Buffer.BlockCopy(signature, 0, blob, data.Length, SignatureBytes);
            return Prefix + Group(ToBase32(blob));
        }

        // ---------------------------------------------------------------- kalitni o'qish (plaginda)

        /// <summary>Kalitni o'qiydi va imzosini tekshiradi (kompyuter va muddat — <see cref="LicenseManager"/>da).</summary>
        public static bool TryRead(string key, ECDsa publicKey, out LicenseInfo info, out string error)
        {
            info = null;
            error = "Kalit noto'g'ri. Uni to'liq nusxalab qo'ying.";

            // Chiziq, bo'sh joy va katta-kichik harf ahamiyatsiz; "RM" boshlanmasi ham shart emas
            var compact = new StringBuilder();
            foreach (char c in (key ?? string.Empty).ToUpperInvariant())
            {
                if (c != '-' && !char.IsWhiteSpace(c))
                    compact.Append(c);
            }
            string text = compact.ToString();
            if (text.StartsWith("RM", StringComparison.Ordinal))
                text = text.Substring(2); // ma'lumot doim "0" bilan boshlanadi, shuning uchun adashmaydi
            if (!TryFromBase32(text, out byte[] blob) || blob.Length < 1 + MachineBytes + 4 + 1 + SignatureBytes)
                return false;

            int dataLength = blob.Length - SignatureBytes;
            var data = new byte[dataLength];
            var signature = new byte[SignatureBytes];
            Buffer.BlockCopy(blob, 0, data, 0, dataLength);
            Buffer.BlockCopy(blob, dataLength, signature, 0, SignatureBytes);

            int p = 0;
            if (data[p++] != FormatVersion)
            {
                error = "Bu kalit plaginning boshqa versiyasi uchun.";
                return false;
            }
            string machine = FormatMachineCode(data, p);
            p += MachineBytes;
            DateTime issued = ReadDay(data, ref p) ?? Epoch;
            DateTime? expires = ReadDay(data, ref p);
            int nameLength = data[p++];
            if (p + nameLength != dataLength)
                return false;
            string name = Encoding.UTF8.GetString(data, p, nameLength);

            bool valid;
            try
            {
                valid = publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256);
            }
            catch (CryptographicException)
            {
                valid = false;
            }
            if (!valid)
                return false;

            info = new LicenseInfo { Name = name, MachineCode = machine, Issued = issued, Expires = expires };
            error = null;
            return true;
        }

        // ---------------------------------------------------------------- ochiq / maxfiy kalit

        public static ECDsa CreateKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

        public static ECDsa PublicKey(string x, string y) => ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Convert.FromBase64String(x), Y = Convert.FromBase64String(y) }
        });

        public static ECDsa PrivateKey(string x, string y, string d) => ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = Convert.FromBase64String(x), Y = Convert.FromBase64String(y) },
            D = Convert.FromBase64String(d)
        });

        // ---------------------------------------------------------------- yordamchilar

        private static void WriteDay(byte[] data, ref int p, DateTime? day)
        {
            int value = day.HasValue ? (int)(day.Value.Date - Epoch.Date).TotalDays + 1 : 0; // 0 — muddatsiz
            if (value < 0 || value > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(day), "Sana 2024-yildan keyin bo'lishi kerak.");
            data[p++] = (byte)(value >> 8);
            data[p++] = (byte)value;
        }

        private static DateTime? ReadDay(byte[] data, ref int p)
        {
            int value = (data[p] << 8) | data[p + 1];
            p += 2;
            return value == 0 ? (DateTime?)null : Epoch.AddDays(value - 1);
        }

        private static string ToBase32(byte[] data)
        {
            var text = new StringBuilder();
            int buffer = 0, bits = 0;
            foreach (byte b in data)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    text.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0)
                text.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            return text.ToString();
        }

        private static bool TryFromBase32(string text, out byte[] data)
        {
            data = null;
            var bytes = new System.Collections.Generic.List<byte>();
            int buffer = 0, bits = 0;
            foreach (char raw in text)
            {
                if (raw == '-' || char.IsWhiteSpace(raw))
                    continue;
                // Crockford: O → 0, I/L → 1
                char c = raw == 'O' ? '0' : raw == 'I' || raw == 'L' ? '1' : raw;
                int value = Alphabet.IndexOf(c);
                if (value < 0)
                    return false;
                buffer = (buffer << 5) | value;
                bits += 5;
                if (bits >= 8)
                {
                    bytes.Add((byte)(buffer >> (bits - 8)));
                    bits -= 8;
                }
            }
            data = bytes.ToArray();
            return true;
        }

        private static string Group(string text)
        {
            var grouped = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0 && i % 5 == 0)
                    grouped.Append('-');
                grouped.Append(text[i]);
            }
            return grouped.ToString();
        }
    }
}
