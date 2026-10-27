using System.Globalization;
using System.Windows.Media;

namespace ReklamaMaket.Core
{
    /// <summary>Rang bilan ishlash yordamchilari (Corel chizmasi va panel ko'rinishi uchun umumiy).</summary>
    public static class ColorMath
    {
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
