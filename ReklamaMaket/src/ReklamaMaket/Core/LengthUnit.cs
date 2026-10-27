using System.Globalization;

namespace ReklamaMaket.Core
{
    /// <summary>Paneldagi uzunlik birligi.</summary>
    public sealed class LengthUnit
    {
        public static readonly LengthUnit Millimeter = new LengthUnit("mm", 1);
        public static readonly LengthUnit Centimeter = new LengthUnit("sm", 10);
        public static readonly LengthUnit Meter = new LengthUnit("m", 1000);

        public static readonly LengthUnit[] All = { Millimeter, Centimeter, Meter };

        private LengthUnit(string name, double millimeters)
        {
            Name = name;
            Millimeters = millimeters;
        }

        public string Name { get; }

        /// <summary>Bir birlikda necha millimetr bor.</summary>
        public double Millimeters { get; }

        public double ToMm(double value) => value * Millimeters;

        public double FromMm(double mm) => mm / Millimeters;

        public override string ToString() => Name;
    }

    /// <summary>Son kiritish/chiqarish (vergul ham, nuqta ham qabul qilinadi).</summary>
    public static class Num
    {
        public static bool TryParse(string text, out double value)
        {
            string normalized = (text ?? string.Empty).Trim().Replace(" ", string.Empty).Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
