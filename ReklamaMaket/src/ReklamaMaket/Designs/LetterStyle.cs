using System;
using System.Windows.Media;

namespace ReklamaMaket.Designs
{
    /// <summary>Harfning qaysi qismi yonadi.</summary>
    [Flags]
    public enum LightZones
    {
        None = 0,

        /// <summary>Old tomoni (yuz) yonadi.</summary>
        Face = 1,

        /// <summary>Orqaga, devorga nur tushadi (kontrajur, halo).</summary>
        Halo = 2,

        /// <summary>Yon tomoni (bort) to'liq yonadi.</summary>
        SideFull = 4,

        /// <summary>Yon tomonida ingichka yonadigan chiziq bor.</summary>
        SidePartial = 8,

        /// <summary>Harf cheti (konturi) neon kabi yonadi.</summary>
        Rim = 16,

        /// <summary>Yuzdagi nuqtalar (LED, lampochka, perforatsiya) yonadi.</summary>
        Dots = 32,

        /// <summary>Yuzdagi lazer kesma naqsh orqali nur chiqadi.</summary>
        Pattern = 64
    }

    /// <summary>Yuz (old tomoni) materiali — kunduzgi ko'rinishni belgilaydi.</summary>
    public enum FaceMaterial
    {
        /// <summary>Sutsimon akril — tekis rang.</summary>
        Acrylic,

        /// <summary>Metall (alyumin, po'lat, bo'yalgan) — gradient bilan.</summary>
        Metal,

        /// <summary>Yaxlit shaffof/rangli akril — biroz shaffof.</summary>
        ClearAcrylic,

        /// <summary>Teshikli (perforatsiyali) metall.</summary>
        Perforated,

        /// <summary>Lazer bilan naqsh kesilgan metall.</summary>
        Pattern
    }

    /// <summary>Logo fonga o'yilganmi (alyukobond) yoki alohida hajmli harfmi.</summary>
    public enum CutMode
    {
        /// <summary>Alohida hajmli harflar (channel letters).</summary>
        None,

        /// <summary>Yuzdan logo o'yib chiqarilgan, o'yiqqa akril qo'yilgan, ichidan yonadi.</summary>
        RoutedLit,

        /// <summary>Qalin akril harf o'yiqdan o'tib, yuzdan chiqib turadi (push-through).</summary>
        PushThrough,

        /// <summary>O'yib chiqarilgan, orqasidan boshqa rangli panel ko'rinadi (yonmaydi).</summary>
        RoutedBacking,

        /// <summary>Gravirovka — yuzga o'yib yozilgan, o'yiqda alyuminiy ko'rinadi (yonmaydi).</summary>
        Engraved
    }

    /// <summary>
    /// Logo dizayni: hajmli harf (channel letter) yoki fonga o'yilgan logo.
    /// Barcha turlar <see cref="LetterStyles.All"/> ro'yxatida.
    /// </summary>
    public sealed class LetterStyle
    {
        public LetterStyle(string name, string englishName, string description, LightZones light, FaceMaterial face,
            Color faceColor, Color returnColor, Color lightColor, double trimMm, double depthMm,
            string faceLabel, string returnLabel, string promptDetails)
        {
            Name = name;
            EnglishName = englishName;
            Description = description;
            Light = light;
            Face = face;
            FaceColor = faceColor;
            ReturnColor = returnColor;
            LightColor = lightColor;
            TrimMm = trimMm;
            DefaultDepth = depthMm;
            FaceLabel = faceLabel;
            ReturnLabel = returnLabel;
            PromptDetails = promptDetails;
        }

        /// <summary>Paneldagi nomi (o'zbekcha).</summary>
        public string Name { get; }

        /// <summary>Inglizcha nomi — panelda ham, ChatGPT so'rovida ham ishlatiladi.</summary>
        public string EnglishName { get; }

        public string Description { get; }
        public LightZones Light { get; }
        public FaceMaterial Face { get; }
        public Color FaceColor { get; }

        /// <summary>Bort (yon tomoni) rangi.</summary>
        public Color ReturnColor { get; }

        /// <summary>LED nuri rangi.</summary>
        public Color LightColor { get; }

        /// <summary>Old tomondan ko'rinadigan chet (trim, bort qirrasi) eni, mm. 0 — ko'rinmaydi.</summary>
        public double TrimMm { get; }

        /// <summary>Standart qalinligi (chuqurligi), mm.</summary>
        public double DefaultDepth { get; }

        /// <summary>Yuzning qirrasi qiya (fasetka).</summary>
        public bool Bevel { get; set; }

        /// <summary>Harf devordan distansiyada turadi (kontrajur uchun shart).</summary>
        public bool OnStandoffs { get; set; }

        /// <summary>Logo fonga o'yiladi (alyukobond) — alohida harf emas.</summary>
        public CutMode Cut { get; set; }

        public bool IsCut => Cut != CutMode.None;

        /// <summary>Ro'yxatdagi guruh nomi.</summary>
        public string Group => IsCut ? "Fonga o'yilgan logo (alyukobond)" : "Hajmli harflar (channel letters)";

        /// <summary>Paneldagi rang va qalinlik yozuvlari (o'yilgan logoda ma'nosi boshqacha).</summary>
        public string FaceColorLabel { get; set; } = "Yuz";
        public string ReturnColorLabel { get; set; } = "Bort";
        public string DepthLabel { get; set; } = "Qalinligi";

        /// <summary>Push-through harfning yuzdan chiqib turishi, mm.</summary>
        public double Protrusion(double depth) => System.Math.Max(depth - 5, 3);

        /// <summary>Kesim sxemasidagi yozuvlar.</summary>
        public string FaceLabel { get; }
        public string ReturnLabel { get; }

        /// <summary>ChatGPT uchun konstruksiya tavsifi (inglizcha).</summary>
        public string PromptDetails { get; }

        public bool Illuminated => Light != LightZones.None;

        public bool Has(LightZones zone) => (Light & zone) != 0;

        public override string ToString() => Name;
    }

    /// <summary>
    /// Harf turlari ro'yxati (yuborilgan rasmlar asosida). Yangi tur qo'shish uchun shu ro'yxatga
    /// bitta element yozing — panel, Corel chizmasi va ChatGPT so'rovi avtomatik ishlaydi.
    /// </summary>
    public static class LetterStyles
    {
        private static Color Rgb(int hex) => Color.FromRgb((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);

        private static readonly Color White = Rgb(0xFFFFFF);
        private static readonly Color Warm = Rgb(0xFFC861);

        public static readonly LetterStyle[] All =
        {
            new LetterStyle("Old tomoni yonadigan", "Front-lit channel letters",
                "Sutsimon akril yuz ichidan LED bilan yonadi, bort — bo'yalgan alyumin (rasmdagi YJ-N002).",
                LightZones.Face, FaceMaterial.Acrylic, White, Rgb(0x2F3A45), White, 8, 80,
                "Yuz: sutsimon akril 3 mm", "Bort: alyumin, bo'yalgan",
                "front-lit channel letters: translucent acrylic faces lit from inside by LEDs, painted aluminium returns and a thin trim cap around each face"),

            new LetterStyle("Qiya qirrali, old tomoni yonadi", "Face-lit letters with bevel edge",
                "Akril yuzning qirrasi qiya (fasetkali), yuz yonadi (YJ-N001).",
                LightZones.Face, FaceMaterial.Acrylic, White, Rgb(0xC9CDD2), White, 0, 60,
                "Yuz: akril, qirrasi qiya", "Bort: alyumin, kumush rang",
                "face-lit acrylic letters with a bevelled (chamfered) face edge and satin silver returns")
            { Bevel = true },

            new LetterStyle("Oq yuz, qora bort", "Black & white face-lit letters",
                "Oq akril yuz yonadi, bort qora (YJ-N007).",
                LightZones.Face, FaceMaterial.Acrylic, White, Rgb(0x141414), White, 4, 60,
                "Yuz: oq akril", "Bort: qora akril/alyumin",
                "face-lit letters with a white acrylic face and black returns; only the white face glows"),

            new LetterStyle("Yaxlit akril, yumaloq qirra", "Solid acrylic letters, round edge, fully lit",
                "Butun harf sutsimon akrildan, qirralari yumaloq — yuzi ham, yoni ham bir tekis yonadi (YJ-N003).",
                LightZones.Face | LightZones.SideFull, FaceMaterial.ClearAcrylic, Rgb(0xF4F6F8), Rgb(0xE6EAEE), White, 3, 50,
                "Yuz va bort: yaxlit sutsimon akril", "Qirra: yumaloq",
                "solid milky-white acrylic letters with rounded edges; the whole letter body (face and sides) glows evenly"),

            new LetterStyle("Yaxlit rangli akril, to'g'ri qirra", "Solid coloured acrylic letters, square edge, fully lit",
                "Rangli yaxlit akril, qirralari to'g'ri burchak, butun harf yonadi (YJ-N004).",
                LightZones.Face | LightZones.SideFull, FaceMaterial.ClearAcrylic, Rgb(0xF05A1A), Rgb(0xB8400F), White, 3, 50,
                "Yuz va bort: rangli akril", "Qirra: to'g'ri burchak",
                "solid coloured translucent acrylic letters with sharp square edges; the whole letter glows in its colour"),

            new LetterStyle("Old + orqa yonadigan", "Face & halo-lit letters",
                "Yuz yonadi va orqaga ham nur chiqadi — devorda yorug' kontur paydo bo'ladi (YJ-N008).",
                LightZones.Face | LightZones.Halo, FaceMaterial.Acrylic, White, Rgb(0xEDEDED), White, 3, 70,
                "Yuz: sutsimon akril", "Bort: alyumin, orqasi shaffof akril",
                "letters lit on the face and also from the back (halo) so light glows on the wall behind; mounted about 30 mm off the wall on spacers")
            { OnStandoffs = true },

            new LetterStyle("Orqadan yonadigan (kontrajur)", "Halo-lit (reverse channel) letters",
                "Yuz va bort metall, nur orqaga — devorga tushib harf atrofida yorug' kontur hosil qiladi.",
                LightZones.Halo, FaceMaterial.Metal, Rgb(0x2A2A2A), Rgb(0x2A2A2A), White, 0, 60,
                "Yuz: metall (yonmaydi)", "Bort: metall, orqasi shaffof akril",
                "halo-lit reverse channel letters: solid metal face and returns, LEDs shine backwards onto the wall creating a soft glowing outline; letters stand 30–40 mm off the wall")
            { OnStandoffs = true },

            new LetterStyle("Yon tomoni qisman yonadi", "Partial side-lit letters",
                "Metall yuz, bortda orqa tomonga yaqin ingichka yonadigan chiziq bor (YJ-N011).",
                LightZones.SidePartial, FaceMaterial.Metal, Rgb(0x6B6F74), Rgb(0x6B6F74), White, 3, 60,
                "Yuz: metall", "Bort: metall + yonadigan akril chiziq",
                "metal-faced letters whose returns have a narrow translucent band near the back, so a thin line of light shows along the sides"),

            new LetterStyle("Old + yon qisman yonadi", "Face & partial side-lit letters",
                "Akril yuz yonadi, bortda ham ingichka yonadigan chiziq (YJ-N013).",
                LightZones.Face | LightZones.SidePartial, FaceMaterial.Acrylic, White, Rgb(0xEDEDED), White, 3, 60,
                "Yuz: sutsimon akril", "Bort: alyumin + yonadigan akril chiziq",
                "face-lit acrylic letters whose returns also have a narrow glowing band"),

            new LetterStyle("Yon tomoni to'liq yonadi", "Side-lit letters (opaque face)",
                "Yuz qora va yonmaydi, bort shaffof — kechasi faqat yon tomonlari yonadi (YJ-N015).",
                LightZones.SideFull, FaceMaterial.Metal, Rgb(0x151515), Rgb(0xF2F2F2), White, 4, 50,
                "Yuz: qora, yonmaydi", "Bort: oq shaffof akril",
                "letters with an opaque black face and translucent white returns; at night only the sides glow"),

            new LetterStyle("Hamma tomoni yonadi", "Front & side fully lit letters",
                "Yuz ham, bort ham shaffof akril — butun harf yonadi (YJ-N016).",
                LightZones.Face | LightZones.SideFull, FaceMaterial.ClearAcrylic, Rgb(0xF7F7F7), Rgb(0xF0F0F0), White, 3, 50,
                "Yuz: sutsimon akril", "Bort: sutsimon akril",
                "letters made fully of milky acrylic — face and sides glow brightly"),

            new LetterStyle("Ochiq LED (skelet)", "Exposed LED (skeleton) letters",
                "Oq harf yuziga kichik ochiq LED nuqtalar qo'yilgan (YJ-N010).",
                LightZones.Dots, FaceMaterial.Acrylic, White, Rgb(0xE9E9E9), White, 3, 60,
                "Yuz: oq plastik + ochiq LED", "Bort: oq alyumin",
                "white letters with small exposed LED points set into the face in rows"),

            new LetterStyle("Lampochkali (marquee)", "Marquee bulb letters",
                "Bo'yalgan metall harf yuziga dumaloq lampochkalar o'rnatilgan.",
                LightZones.Dots, FaceMaterial.Metal, Rgb(0x8E2B1E), Rgb(0x5A1A12), Warm, 6, 100,
                "Yuz: bo'yalgan metall + lampochkalar", "Bort: bo'yalgan metall",
                "marquee letters: painted metal letters with round warm-white light bulbs set into the face"),

            new LetterStyle("Perforatsiyali + cheti yonadi", "Perforated pixel-LED letters with lit rim",
                "Qora teshikli metall yuz — teshiklarda LED piksellar, harf cheti neon kabi yonadi.",
                LightZones.Dots | LightZones.Rim, FaceMaterial.Perforated, Rgb(0x1C1C1C), Rgb(0xB88A3B), Warm, 8, 80,
                "Yuz: perforatsiyali qora metall + LED", "Bort: metall, cheti yonadigan akril",
                "letters with a black perforated metal face showing tiny LED pixels through the holes and a glowing neon-like rim around the letter edge"),

            new LetterStyle("Naqshli lazer kesma yuz", "Laser-cut pattern face letters",
                "To'q metall yuzda lazer bilan naqsh kesilgan, naqsh orqali iliq nur chiqadi.",
                LightZones.Pattern, FaceMaterial.Pattern, Rgb(0x2A2522), Rgb(0x2A2522), Warm, 0, 60,
                "Yuz: naqshli metall + akril qatlam", "Bort: to'q metall",
                "letters with a dark metal face laser-cut in a decorative lattice pattern; warm light shines through the pattern from an acrylic layer behind"),

            new LetterStyle("Akril harf, yoritilmaydi", "Non-illuminated acrylic letters on standoffs",
                "Qalin shaffof akrildan kesilgan harf, yuzi qora; devordan biroz chiqib turadi. Yoritilmaydi.",
                LightZones.None, FaceMaterial.ClearAcrylic, Rgb(0x1A1A1A), Rgb(0xDCE5EA), White, 0, 20,
                "Yuz: qora plyonka", "Tana: shaffof akril",
                "non-illuminated letters cut from thick clear acrylic with a black face, mounted slightly off the wall")
            { OnStandoffs = true },

            new LetterStyle("Metall harf, yoritilmaydi", "Non-illuminated metal letters",
                "Zanglamaydigan po'lat yoki tilla rang metall harf. Yoritilmaydi.",
                LightZones.None, FaceMaterial.Metal, Rgb(0xC9A44C), Rgb(0xA8873A), White, 0, 30,
                "Yuz: metall", "Bort: metall",
                "non-illuminated brushed metal letters"),

            // ---- Fonga o'yilgan logo (alyukobond)
            new LetterStyle("O'yib chiqarilgan + akril, yonadi", "Routed (cut-out) face with flush acrylic, backlit",
                "Alyukobond yuzdan logo frezerda o'yib chiqariladi, o'yiqqa yuz bilan bir tekis akril qo'yiladi, ichidan LED yoritadi " +
                "(Cocktail, ABIERTO, OPEN rasmlari). Rangli akril yoki logoning o'z ranglari.",
                LightZones.Face, FaceMaterial.Acrylic, White, White, White, 0, 10,
                "Akril, yuz bilan bir tekis", "Yuz: alyukobond 4 mm, logo o'yilgan",
                "the logo is CNC-routed (cut out) of the opaque aluminium composite face of a light box, and the cut-outs are filled " +
                "with milky acrylic that sits flush with the face; LEDs inside the box make only the logo glow")
            { Cut = CutMode.RoutedLit, FaceColorLabel = "Akril", ReturnColorLabel = "—", DepthLabel = "Akril qalinligi" },

            new LetterStyle("Push-through: akril harf chiqib turadi", "Push-through acrylic letters",
                "Qalin akril harf yuzdagi o'yiqdan o'tib, tashqariga chiqib turadi; yuziga rangli plyonka, qirralari yonadi (\"akti\" rasmi).",
                LightZones.Face | LightZones.SideFull, FaceMaterial.ClearAcrylic, Rgb(0x1FC7D4), White, White, 4, 20,
                "Akril harf, yuzdan chiqib turadi", "Yuz: alyukobond 4 mm, logo o'yilgan",
                "thick acrylic letters pushed through routed openings in the aluminium composite face of a light box " +
                "so they protrude from it; the letter faces are covered with coloured film and the clear edges glow brightly")
            { Cut = CutMode.PushThrough, FaceColorLabel = "Plyonka", ReturnColorLabel = "Qirra", DepthLabel = "Akril qalinligi" },

            new LetterStyle("O'yib chiqarilgan, orqasida rangli panel", "Routed face over a contrasting backing panel",
                "Old panel o'yib chiqariladi, orqasidagi boshqa rangli panel ko'rinadi — ikki qatlamli (HORUS rasmidagi kichik yozuv).",
                LightZones.None, FaceMaterial.Acrylic, Rgb(0x3FA9F5), White, White, 0, 4,
                "Orqa panel (ikkinchi qatlam)", "Yuz: alyukobond 4 mm, logo o'yilgan",
                "the logo is cut out through the front aluminium composite panel, revealing a second panel of a different colour mounted " +
                "behind it (two-layer sign, not illuminated)")
            { Cut = CutMode.RoutedBacking, FaceColorLabel = "Orqa panel", ReturnColorLabel = "—", DepthLabel = "Panel qalinligi" },

            new LetterStyle("Gravirovka (o'yib yozilgan)", "Engraved (V-groove) logo",
                "Logo alyukobond yuziga frezer bilan o'yib yoziladi — o'yiqda kumush alyuminiy ko'rinadi.",
                LightZones.None, FaceMaterial.Metal, Rgb(0xC9CED3), White, White, 0, 2,
                "O'yiq: alyuminiy ko'rinadi", "Yuz: alyukobond 4 mm",
                "the logo is engraved (V-groove milled) into the aluminium composite face so the bare silver aluminium shows in the engraving; not illuminated")
            { Cut = CutMode.Engraved, FaceColorLabel = "O'yiq", ReturnColorLabel = "—", DepthLabel = "O'yiq chuqurligi" },
        };
    }
}
