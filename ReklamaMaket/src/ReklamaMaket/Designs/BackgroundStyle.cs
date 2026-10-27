using System.Windows.Media;
using ReklamaMaket.Core;

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
        AcrylicPlate,

        /// <summary>Gulli (perforatsiyali) temir list.</summary>
        PatternSheet,

        /// <summary>Temir ramkadagi setka.</summary>
        FramedMesh,

        /// <summary>Yog'och ramkali setka, orqasi yonadi.</summary>
        WoodFrameMesh,

        /// <summary>Gorizontal temir reykalar (lamel) kronshteynlarda.</summary>
        HorizontalSlats,

        /// <summary>Vertikal reykali (qovurg'ali) panel.</summary>
        VerticalSlats,

        /// <summary>Alyukobond quti — yuzi shaffof emas, logo yuzga o'yiladi, ichida LED.</summary>
        AcpBox
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

        /// <summary>Fon (list, panel, reyka) rangi.</summary>
        public Color DefaultColor { get; }

        /// <summary>Standart qalinligi, mm (yonadigan fonda — orqadagi quti chuqurligi).</summary>
        public double DefaultDepth { get; }

        /// <summary>ChatGPT uchun tavsif (inglizcha).</summary>
        public string PromptDetails { get; }

        /// <summary>Gul (perforatsiya/setka) tanlanadi.</summary>
        public bool HasPattern => Kind == BackgroundKind.PatternSheet || Kind == BackgroundKind.FramedMesh || Kind == BackgroundKind.WoodFrameMesh;

        public bool HasSlats => Kind == BackgroundKind.HorizontalSlats || Kind == BackgroundKind.VerticalSlats;

        /// <summary>Logo o'yib qilinadigan alyukobond yuz (panel, kasseta, quti).</summary>
        public bool CanBeCut => Kind == BackgroundKind.Panel || Kind == BackgroundKind.Cassette ||
                                Kind == BackgroundKind.Lightbox || Kind == BackgroundKind.AcpBox;

        /// <summary>Chetida o'yilgan ramka chizig'i qo'yish mumkin.</summary>
        public bool CanHaveBorder => CanBeCut;

        /// <summary>Qaysi joyi yonishini tanlash mumkin.</summary>
        public bool SupportsLighting => HasPattern || HasSlats;

        /// <summary>Gul tanlanmaganda standart gul (<see cref="PatternStyles.All"/> indeksi).</summary>
        public int DefaultPattern { get; set; }

        public BackgroundLight DefaultLight { get; set; } = BackgroundLight.None;

        /// <summary>Ramka rangi (ramkali fonlar uchun).</summary>
        public Color FrameColor { get; set; } = Color.FromRgb(0x4A, 0x4F, 0x55);

        /// <summary>Ramka eni, mm (0 — ramkasiz).</summary>
        public double FrameWidth { get; set; }

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

            new BackgroundStyle(BackgroundKind.AcpBox, "Alyukobond quti (yuzi o'yiladi)", "Aluminium composite light box with routed face",
                "Alyukobonddan yasalgan quti: yuzi yonmaydi, logo yuzga o'yiladi va ichidagi LED faqat logoni yoritadi.",
                Color.FromRgb(0x1E, 0x1E, 0x1E), 80,
                "on a box made of aluminium composite panels with an opaque face and LEDs inside"),

            new BackgroundStyle(BackgroundKind.PatternSheet, "Gulli temir list (perforatsiya)", "Perforated metal sheet with pattern",
                "Lazer/press bilan gul teshilgan temir list. Orqasiga LED qo'yilsa, teshiklar orqali yonadi.",
                Color.FromRgb(0x2B, 0x2F, 0x33), 60,
                "on a powder-coated perforated metal sheet")
            { DefaultPattern = 0 },

            new BackgroundStyle(BackgroundKind.FramedMesh, "Ramkadagi setka (temir ramka)", "Expanded metal mesh in a steel frame",
                "Profil quvurdan payvandlangan ramka ichida romb (prosechka) yoki boshqa setka.",
                Color.FromRgb(0x9A, 0x9F, 0xA4), 40,
                "on an expanded metal mesh panel welded into a square-tube steel frame with cross bars")
            { DefaultPattern = 21, FrameWidth = 40, FrameColor = Color.FromRgb(0x3E, 0x42, 0x47) },

            new BackgroundStyle(BackgroundKind.WoodFrameMesh, "Yog'och ramkali setka (orqadan yonadi)", "Backlit mesh in a wooden frame",
                "Yog'och ramka, ichida setka, orqasida LED — chetlari bo'ylab iliq nur.",
                Color.FromRgb(0xCF, 0xC8, 0xBC), 60,
                "on a backlit metal mesh panel set in a dark stained wooden frame")
            { DefaultPattern = 28, DefaultLight = BackgroundLight.Perimeter, FrameWidth = 60, FrameColor = Color.FromRgb(0x5A, 0x38, 0x22) },

            new BackgroundStyle(BackgroundKind.HorizontalSlats, "Gorizontal temir reykalar (lamel)", "Horizontal metal slats (louvers)",
                "Kronshteynlarga mahkamlangan gorizontal profil reykalar, oralig'idan devor ko'rinadi.",
                Color.FromRgb(0x4A, 0x50, 0x56), 60,
                "on horizontal powder-coated metal slats (louvers) mounted on vertical brackets in front of the wall, with gaps between the slats"),

            new BackgroundStyle(BackgroundKind.VerticalSlats, "Vertikal reykali panel", "Vertical slatted (fluted) panel",
                "Fasaddagi vertikal reykali qoplama — harflar reykalar ustida.",
                Color.FromRgb(0x2A, 0x2E, 0x34), 40,
                "on a dark vertical slatted (fluted) facade cladding"),
        };
    }
}
