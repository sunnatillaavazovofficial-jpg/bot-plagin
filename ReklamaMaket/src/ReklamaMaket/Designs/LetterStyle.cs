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

    /// <summary>
    /// Hajmli harf (channel letter) turi. Barcha turlar <see cref="LetterStyles.All"/> ro'yxatida.
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
        };
    }
}
