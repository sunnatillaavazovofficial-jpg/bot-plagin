using System.Windows.Media;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Core
{
    /// <summary>Logoning fon ichidagi joylashuvi.</summary>
    public enum LogoAlign
    {
        Center,
        Left,
        Right,
        Top,
        Bottom
    }

    /// <summary>
    /// Paneldagi barcha sozlamalar. O'lchamlar doim millimetrda (haqiqiy o'lcham),
    /// panel ularni tanlangan birlikka (mm / sm / m) o'girib ko'rsatadi.
    /// </summary>
    public sealed class SignSettings
    {
        // ---- Logo (harflar)
        public LetterStyle Letter { get; set; } = LetterStyles.All[0];

        /// <summary>Logoning haqiqiy eni, mm.</summary>
        public double LogoWidth { get; set; } = 2000;

        /// <summary>Logoning haqiqiy bo'yi, mm.</summary>
        public double LogoHeight { get; set; } = 600;

        /// <summary>Harf qalinligi (chuqurligi), mm.</summary>
        public double Depth { get; set; } = 80;

        public Color FaceColor { get; set; } = Colors.White;

        /// <summary>Bort (yon tomoni) rangi.</summary>
        public Color ReturnColor { get; set; } = Colors.Gray;

        public Color LightColor { get; set; } = Colors.White;

        /// <summary>Yuz uchun logoning o'z ranglarini ishlatish.</summary>
        public bool KeepLogoColors { get; set; }

        // ---- Fon
        public BackgroundStyle Background { get; set; } = BackgroundStyles.All[0];

        public Color BackgroundColor { get; set; } = Colors.White;

        /// <summary>Fon o'lchami = logo + chetdan joy.</summary>
        public bool AutoBackgroundSize { get; set; } = true;

        public double BackgroundWidth { get; set; } = 2400;
        public double BackgroundHeight { get; set; } = 1000;

        /// <summary>Fon qalinligi, mm.</summary>
        public double BackgroundDepth { get; set; }

        /// <summary>Logo bilan fon cheti orasidagi joy, mm.</summary>
        public double Margin { get; set; } = 200;

        public LogoAlign Align { get; set; } = LogoAlign.Center;

        // ---- Corel
        /// <summary>Corelda chizish masshtabi: 1:Scale.</summary>
        public int Scale { get; set; } = 10;

        public bool DrawNight { get; set; } = true;
        public bool DrawSection { get; set; } = true;
        public bool DrawDimensions { get; set; } = true;
        public bool NewPage { get; set; } = true;
    }
}
