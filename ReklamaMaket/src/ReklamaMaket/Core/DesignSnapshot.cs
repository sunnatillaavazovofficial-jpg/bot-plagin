using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Core
{
    /// <summary>
    /// Oxirgi chizilgan dizayn: %APPDATA%\ReklamaMaket\last-design.json va logo rasmi (last-logo.png).
    /// Reklama Maket har chizishda saqlaydi, Reklama Rasxod plagini shu dizaynni ochib, material rasxodini hisoblaydi.
    /// Harf, fon va gul nomi bo'yicha (inglizcha nomi) saqlanadi.
    /// </summary>
    [DataContract]
    public sealed class DesignSnapshot
    {
        [DataMember] public DateTime Saved { get; set; }
        [DataMember] public string Letter { get; set; }
        [DataMember] public string Background { get; set; }
        [DataMember] public string Pattern { get; set; }
        [DataMember] public string BackgroundLight { get; set; }
        [DataMember] public string Align { get; set; }

        [DataMember] public double LogoWidth { get; set; }
        [DataMember] public double LogoHeight { get; set; }
        [DataMember] public double Depth { get; set; }
        [DataMember] public bool AutoBackgroundSize { get; set; }
        [DataMember] public double BackgroundWidth { get; set; }
        [DataMember] public double BackgroundHeight { get; set; }
        [DataMember] public double BackgroundDepth { get; set; }
        [DataMember] public double Margin { get; set; }
        [DataMember] public bool KeepLogoColors { get; set; }
        [DataMember] public bool EngravedBorder { get; set; }

        [DataMember] public string FaceColor { get; set; }
        [DataMember] public string ReturnColor { get; set; }
        [DataMember] public string LightColor { get; set; }
        [DataMember] public string BackgroundColor { get; set; }
        [DataMember] public Dictionary<string, string> DetailColors { get; set; }
        [DataMember] public Dictionary<string, double> DetailSizes { get; set; }

        /// <summary>Logoning Corel'dagi o'lchami (mm) va obyektlar soni.</summary>
        [DataMember] public double LogoCorelWidth { get; set; }
        [DataMember] public double LogoCorelHeight { get; set; }
        [DataMember] public int LogoCount { get; set; }

        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReklamaMaket");

        public static string FilePath => Path.Combine(Folder, "last-design.json");

        public static string LogoPath => Path.Combine(Folder, "last-logo.png");

        public static DesignSnapshot From(SignSettings s, LogoInfo logo)
        {
            return new DesignSnapshot
            {
                Saved = DateTime.Now,
                Letter = s.Letter.EnglishName,
                Background = s.Background.EnglishName,
                Pattern = s.Pattern.Name,
                BackgroundLight = s.BackgroundLight.ToString(),
                Align = s.Align.ToString(),
                LogoWidth = s.LogoWidth,
                LogoHeight = s.LogoHeight,
                Depth = s.Depth,
                AutoBackgroundSize = s.AutoBackgroundSize,
                BackgroundWidth = s.BackgroundWidth,
                BackgroundHeight = s.BackgroundHeight,
                BackgroundDepth = s.BackgroundDepth,
                Margin = s.Margin,
                KeepLogoColors = s.KeepLogoColors,
                EngravedBorder = s.EngravedBorder,
                FaceColor = ColorMath.ToHex(s.FaceColor),
                ReturnColor = ColorMath.ToHex(s.ReturnColor),
                LightColor = ColorMath.ToHex(s.LightColor),
                BackgroundColor = ColorMath.ToHex(s.BackgroundColor),
                DetailColors = s.DetailColors.ToDictionary(pair => pair.Key.ToString(), pair => ColorMath.ToHex(pair.Value)),
                DetailSizes = s.DetailSizes.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                LogoCorelWidth = logo?.WidthMm ?? 0,
                LogoCorelHeight = logo?.HeightMm ?? 0,
                LogoCount = logo?.Count ?? 0
            };
        }

        /// <summary>Dizaynni va logo rasmini saqlaydi. Xato bo'lsa jim o'tadi — chizishga xalaqit bermasin.</summary>
        public static void TrySave(SignSettings s, LogoInfo logo)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                using (FileStream stream = File.Create(FilePath))
                    Serializer().WriteObject(stream, From(s, logo));

                if (logo?.Preview != null)
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(logo.Preview));
                    using (FileStream stream = File.Create(LogoPath))
                        encoder.Save(stream);
                }
                else if (File.Exists(LogoPath))
                {
                    File.Delete(LogoPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReklamaMaket: dizaynni saqlab bo'lmadi: " + ex);
            }
        }

        /// <summary>Saqlangan dizayn; yo'q yoki o'qib bo'lmasa — null.</summary>
        public static DesignSnapshot Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;
                using (FileStream stream = File.OpenRead(FilePath))
                    return (DesignSnapshot)Serializer().ReadObject(stream);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReklamaMaket: saqlangan dizaynni o'qib bo'lmadi: " + ex);
                return null;
            }
        }

        /// <summary>Saqlangan logo rasmi (shaffof fonli PNG); yo'q bo'lsa null.</summary>
        public BitmapSource LoadLogo()
        {
            if (!File.Exists(LogoPath))
                return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(LogoPath);
            image.EndInit();
            image.Freeze();
            return image;
        }

        public LetterStyle FindLetter() => LetterStyles.All.FirstOrDefault(l => l.EnglishName == Letter);

        public BackgroundStyle FindBackground() => BackgroundStyles.All.FirstOrDefault(b => b.EnglishName == Background);

        public PatternStyle FindPattern() => PatternStyles.All.FirstOrDefault(p => p.Name == Pattern);

        public static Color ParseColor(string hex, Color fallback) => ColorMath.TryParseHex(hex, out Color color) ? color : fallback;

        public Dictionary<Detail, Color> ParseDetailColors()
        {
            var result = new Dictionary<Detail, Color>();
            foreach (KeyValuePair<string, string> pair in DetailColors ?? new Dictionary<string, string>())
            {
                if (Enum.TryParse(pair.Key, out Detail detail) && ColorMath.TryParseHex(pair.Value, out Color color))
                    result[detail] = color;
            }
            return result;
        }

        public Dictionary<Detail, double> ParseDetailSizes()
        {
            var result = new Dictionary<Detail, double>();
            foreach (KeyValuePair<string, double> pair in DetailSizes ?? new Dictionary<string, double>())
            {
                if (Enum.TryParse(pair.Key, out Detail detail))
                    result[detail] = pair.Value;
            }
            return result;
        }

        private static DataContractJsonSerializer Serializer() =>
            new DataContractJsonSerializer(typeof(DesignSnapshot), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
    }
}
