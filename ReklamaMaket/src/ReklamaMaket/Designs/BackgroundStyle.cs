using System.Windows.Media;

namespace ReklamaMaket.Designs
{
    public enum BackgroundKind
    {
        /// <summary>Fon yo'q — harflar to'g'ridan-to'g'ri devorga.</summary>
        Wall,

        /// <summary>Tekis kompozit panel.</summary>
        Panel,

        /// <summary>Chetlari bukilgan kompozit kasseta.</summary>
        Cassette,

        /// <summary>Ichidan yoritiladigan quti.</summary>
        Lightbox,

        /// <summary>Distansiyada turgan akril plita.</summary>
        AcrylicPlate
    }

    /// <summary>Logo orqasidagi fon (podloshka) turi. Ro'yxat — <see cref="BackgroundStyles.All"/>.</summary>
    public sealed class BackgroundStyle
    {
        public BackgroundStyle(BackgroundKind kind, string name, string englishName, string description,
            Color defaultColor, double defaultDepth, string promptDetails)
        {
            Kind = kind;
            Name = name;
            EnglishName = englishName;
            Description = description;
            DefaultColor = defaultColor;
            DefaultDepth = defaultDepth;
            PromptDetails = promptDetails;
        }

        public BackgroundKind Kind { get; }
        public string Name { get; }
        public string EnglishName { get; }
        public string Description { get; }
        public Color DefaultColor { get; }

        /// <summary>Standart qalinligi, mm.</summary>
        public double DefaultDepth { get; }

        /// <summary>ChatGPT uchun tavsif (inglizcha).</summary>
        public string PromptDetails { get; }

        public bool Glows => Kind == BackgroundKind.Lightbox;

        public override string ToString() => Name;
    }

    public static class BackgroundStyles
    {
        public static readonly BackgroundStyle[] All =
        {
            new BackgroundStyle(BackgroundKind.Wall, "Fonsiz (to'g'ridan-to'g'ri devorga)", "Mounted directly on the wall",
                "Harflar fasad devoriga o'zi mahkamlanadi. Rang — devor rangi.",
                Color.FromRgb(0xD8, 0xCF, 0xC2), 0,
                "mounted directly on the building wall without a backing panel"),

            new BackgroundStyle(BackgroundKind.Panel, "Kompozit panel (ACP)", "Aluminium composite panel",
                "Tekis alyumin kompozit panel (4 mm), harflar uning ustida.",
                Colors.White, 4,
                "on a flat aluminium composite panel (ACP) fixed to the facade"),

            new BackgroundStyle(BackgroundKind.Cassette, "Kasseta (bortli panel)", "Composite cassette panel",
                "Chetlari bukilgan kompozit kasseta — qalinroq va tekis ko'rinadi.",
                Color.FromRgb(0x1E, 0x24, 0x30), 40,
                "on a composite cassette panel with folded 40 mm edges"),

            new BackgroundStyle(BackgroundKind.Lightbox, "Laytboks fon (yoritiladigan)", "Illuminated lightbox background",
                "Ichidan yoritiladigan quti, harflar uning yuzida.",
                Colors.White, 100,
                "on an internally illuminated lightbox backing panel with an aluminium profile frame"),

            new BackgroundStyle(BackgroundKind.AcrylicPlate, "Akril plita (distansiyada)", "Clear acrylic plate on standoffs",
                "Shaffof akril plita devordan distansiya boltlarida turadi.",
                Color.FromRgb(0xEA, 0xF3, 0xF7), 10,
                "on a clear acrylic plate mounted on polished steel standoffs"),
        };
    }
}
