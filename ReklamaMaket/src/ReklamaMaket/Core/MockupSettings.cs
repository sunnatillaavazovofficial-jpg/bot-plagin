using System.Windows.Media;

namespace ReklamaMaket.Core
{
    /// <summary>Logoning yuz ichidagi joylashuvi.</summary>
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
    public sealed class MockupSettings
    {
        /// <summary>Konstruksiya eni, mm.</summary>
        public double Width { get; set; } = 3000;

        /// <summary>Konstruksiya bo'yi, mm.</summary>
        public double Height { get; set; } = 800;

        /// <summary>Qalinligi (chuqurligi), mm — 3D ko'rinish va soya uchun.</summary>
        public double Depth { get; set; } = 120;

        /// <summary>Yerdan konstruksiyaning pastki chetigacha balandlik, mm.</summary>
        public double Elevation { get; set; } = 2800;

        /// <summary>Logo bilan yuz cheti orasidagi bo'sh joy, qisqa tomonning foizida.</summary>
        public double MarginPercent { get; set; } = 8;

        /// <summary>Logo hajmi, bo'sh joyni hisobga olgan holda mavjud maydonning foizida.</summary>
        public double LogoSizePercent { get; set; } = 100;

        public LogoAlign Align { get; set; } = LogoAlign.Center;

        /// <summary>Logo orqasidagi fon rangi.</summary>
        public Color Background { get; set; } = Colors.White;

        /// <summary>Karkas / profil / ustun rangi.</summary>
        public Color FrameColor { get; set; } = Color.FromRgb(0xB8, 0xBD, 0xC2);

        /// <summary>Corelda chizish masshtabi: 1:Scale.</summary>
        public int Scale { get; set; } = 10;

        /// <summary>Devor, yer, osmon kabi atrof-muhitni chizish.</summary>
        public bool DrawEnvironment { get; set; } = true;

        /// <summary>Masshtabni his qilish uchun odam siluyeti (1.75 m).</summary>
        public bool DrawPerson { get; set; } = true;

        /// <summary>O'lcham chiziqlari va yozuvlari.</summary>
        public bool DrawDimensions { get; set; } = true;

        /// <summary>Maketni yangi sahifada yaratish.</summary>
        public bool NewPage { get; set; } = true;
    }
}
