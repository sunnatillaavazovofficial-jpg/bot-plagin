using System;
using System.Globalization;
using System.Windows.Media;

namespace ReklamaMaket.Core
{
    /// <summary>Rang bilan ishlash yordamchilari (Corel chizmasi va panel ko'rinishi uchun umumiy).</summary>
    public static class ColorMath
    {
        /// <summary>Setka/reyka oralig'idan ko'rinadigan devor.</summary>
        public static readonly Color WallBehind = Rgb(0xD3D0CB);

        /// <summary>Yonadigan fonning orqasidagi oq diffuzor (kunduzi).</summary>
        public static readonly Color Diffuser = Rgb(0xE6E3DD);

        /// <summary>Po'lat (profil, bolt, kronshteyn).</summary>
        public static readonly Color Steel = Rgb(0xA3A9AF);

        /// <summary>Alyuminiy (gravirovka o'yig'i).</summary>
        public static readonly Color Silver = Rgb(0xC9CED3);

        /// <summary>Kechasi yorug'lik darajasiga mos rang (0 — qorong'i, eng yuqori — nur rangi).</summary>
        public static Color LightLevel(Color behind, Color light, int level, int levels)
        {
            return Mix(Shade(behind, 0.12), light, (double)level / (levels - 1));
        }

        public static Color Rgb(int hex) => Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);

        /// <summary>Rangni qoraytirish (factor &lt; 1) yoki ochish (factor &gt; 1).</summary>
        public static Color Shade(Color color, double factor)
        {
            byte Channel(byte value) => (byte)Num.Clamp(value * factor, 0, 255);
            return Color.FromRgb(Channel(color.R), Channel(color.G), Channel(color.B));
        }

        /// <summary>Yonib turgan rang: oqqa yaqinlashtirilgan.</summary>
        public static Color Lit(Color color)
        {
            byte Channel(byte value) => (byte)(value + (255 - value) * 0.4);
            return Color.FromRgb(Channel(color.R), Channel(color.G), Channel(color.B));
        }

        /// <summary>Ikki rang orasidagi rang (t = 0 — a, t = 1 — b).</summary>
        public static Color Mix(Color a, Color b, double t)
        {
            t = Num.Clamp(t, 0, 1);
            byte Channel(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
            return Color.FromRgb(Channel(a.R, b.R), Channel(a.G, b.G), Channel(a.B, b.B));
        }

        /// <summary>Rangning inglizcha taxminiy nomi va hex kodi — ChatGPT so'rovi uchun.</summary>
        public static string Describe(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double saturation = max <= 0 ? 0 : (max - min) / max;
            string name;
            if (saturation < 0.15)
            {
                name = max > 0.9 ? "white" : max < 0.18 ? "black" : max < 0.45 ? "dark grey" : max < 0.75 ? "grey" : "light grey";
            }
            else
            {
                double hue;
                if (max == r) hue = 60 * (((g - b) / (max - min)) % 6);
                else if (max == g) hue = 60 * ((b - r) / (max - min) + 2);
                else hue = 60 * ((r - g) / (max - min) + 4);
                if (hue < 0) hue += 360;

                if (hue < 15 || hue >= 345) name = "red";
                else if (hue < 40) name = max < 0.6 ? "brown" : "orange";
                else if (hue < 65) name = saturation < 0.75 && max < 0.85 ? "gold" : "yellow";
                else if (hue < 170) name = "green";
                else if (hue < 200) name = "teal";
                else if (hue < 255) name = "blue";
                else if (hue < 290) name = "purple";
                else name = "pink";

                if (max < 0.35 && name != "brown") name = "dark " + name;
            }
            return name + " (" + ToHex(c) + ")";
        }

        public static string ToHex(Color color) => string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);

        public static bool TryParseHex(string text, out Color color)
        {
            color = Colors.White;
            string hex = (text ?? string.Empty).Trim().TrimStart('#');
            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
                return false;

            color = Rgb(value);
            return true;
        }
    }
}
