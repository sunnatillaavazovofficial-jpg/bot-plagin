using System;
using System.Diagnostics;
using System.IO;

namespace ReklamaMaket.Core
{
    /// <summary>
    /// Kutilmagan xatolar jurnali: %APPDATA%\&lt;plagin&gt;\xato.log. Xatoning to'liq matni (qaysi fayl, qaysi qator)
    /// shu yerga yoziladi — muammoni topish uchun shu faylni yuborish kifoya.
    /// </summary>
    public static class ErrorLog
    {
        private const long MaxBytes = 512 * 1024;

        /// <summary>Xatoni yozadi va jurnal fayli yo'lini qaytaradi (yozib bo'lmasa — null).</summary>
        public static string Write(string plugin, Exception ex)
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), plugin);
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, "xato.log");
                if (File.Exists(file) && new FileInfo(file).Length > MaxBytes)
                    File.Delete(file);

                string version = typeof(ErrorLog).Assembly.GetName().Version?.ToString() ?? "?";
                File.AppendAllText(file, string.Format("===== {0:yyyy-MM-dd HH:mm:ss} · {1} {2} · Windows {3}{4}{5}{4}{4}",
                    DateTime.Now, plugin, version, Environment.OSVersion.Version, Environment.NewLine, ex));
                return file;
            }
            catch (Exception logError)
            {
                Debug.WriteLine(plugin + ": xatoni jurnalga yozib bo'lmadi: " + logError);
                return null;
            }
        }

        /// <summary>Panel uchun qisqa xabar: xato matni va jurnal fayli.</summary>
        public static string Describe(string plugin, Exception ex)
        {
            string file = Write(plugin, ex);
            return "Xatolik: " + ex.Message + (file != null ? "\nTafsilot: " + file : string.Empty);
        }
    }
}
