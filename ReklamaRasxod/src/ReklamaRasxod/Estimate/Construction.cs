using System;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;

namespace ReklamaRasxod.Estimate
{
    /// <summary>Harf yuzi materiali.</summary>
    public enum FaceKind
    {
        AcrylicOpal,
        AcrylicColor,
        AcrylicClear,
        Acp,
        Metal,
        Fomax
    }

    /// <summary>Harf bort (yon tomoni) materiali.</summary>
    public enum ReturnKind
    {
        Auto,
        AluminiumTape,
        FomaxStrip,
        AcrylicStrip,
        AcpStrip,
        MetalSheet
    }

    /// <summary>Harf orqasi (zadnik) materiali.</summary>
    public enum BackKind
    {
        Auto,
        Fomax,
        Acp,
        ClearAcrylic,
        Metal,
        None
    }

    /// <summary>Harf ichidagi yorug'lik manbai.</summary>
    public enum LedKind
    {
        None,
        Modules,
        OpenDots,
        Bulbs,
        Pixels
    }

    /// <summary>Listli materiallar qanday hisoblanadi.</summary>
    public enum SheetUnit
    {
        SquareMeters,
        WholeSheets
    }

    /// <summary>Panelning "Konstruksiya" bo'limidagi tanlovlar.</summary>
    public sealed class ConstructionChoice
    {
        public ReturnKind Returns { get; set; } = ReturnKind.Auto;
        public BackKind Back { get; set; } = BackKind.Auto;

        /// <summary>Fomaks qalinligi (harf orqasi, bort tasmasi, yassi harf), mm.</summary>
        public double FomaxThickness { get; set; } = 5;

        /// <summary>Alyukobond qalinligi, mm.</summary>
        public double AcpThickness { get; set; } = 4;

        /// <summary>Karkas profil quvuri, mm (masalan 20×20).</summary>
        public int ProfileA { get; set; } = 20;
        public int ProfileB { get; set; } = 20;

        /// <summary>Alyuminiy / fomaks bort bo'yaladi.</summary>
        public bool PaintReturns { get; set; }

        public SheetUnit SheetUnit { get; set; } = SheetUnit.SquareMeters;
    }

    /// <summary>Tanlangan harf turi qanday yasaladi: yuz, bort, orqa, LED, plyonka, bo'yoq.</summary>
    public sealed class LetterBuild
    {
        public FaceKind Face { get; set; }
        public double FaceThickness { get; set; } = 3;

        /// <summary>Yaxlit harf (ichi bo'sh emas): bort va orqa qism alohida yasalmaydi.</summary>
        public bool Solid { get; set; }

        public ReturnKind Returns { get; set; }
        public BackKind Back { get; set; }

        /// <summary>Bortda ingichka yonadigan akril chiziq (qisman yon nur).</summary>
        public bool AcrylicBand { get; set; }

        /// <summary>Yuzga rangli shaffof orakal (translucent) yopiladi.</summary>
        public bool TranslucentFilm { get; set; }

        /// <summary>Yuzga oddiy (shaffof bo'lmagan) orakal yopiladi.</summary>
        public bool OpaqueFilm { get; set; }

        public bool Trim { get; set; }
        public LedKind Led { get; set; }
        public bool Halo { get; set; }

        /// <summary>Harf cheti bo'ylab neon lenta.</summary>
        public bool RimStrip { get; set; }

        /// <summary>Lazer bilan naqsh kesilgan yuz va uning orqasidagi akril qatlam.</summary>
        public bool PatternFace { get; set; }

        /// <summary>Teshikli (perforatsiyali) yuz.</summary>
        public bool PerforatedFace { get; set; }

        public bool PaintFace { get; set; }
        public bool PaintReturns { get; set; }

        /// <summary>Shpilka bilan mahkamlanadi (metall harf).</summary>
        public bool Studs { get; set; }

        /// <summary>
        /// Harf turidan konstruksiya. Yangi harf turi (<see cref="LetterStyles.All"/>) qo'shilganda shu yerda
        /// alohida ko'rsatish shart emas: yorug'lik zonalari va yuz materialiga qarab avtomatik tanlanadi.
        /// </summary>
        public static LetterBuild For(SignSettings s, ConstructionChoice choice)
        {
            LetterStyle letter = s.Letter;
            var build = new LetterBuild { Halo = letter.Has(LightZones.Halo), RimStrip = letter.Has(LightZones.Rim) };
            bool lightFace = !IsNearWhite(s.FaceColor) || s.KeepLogoColors;

            if (!letter.Illuminated)
            {
                if (letter.Face == FaceMaterial.Metal)
                {
                    // Metall harf: yuz va bort bo'yalgan list, devorga shpilka bilan
                    build.Face = FaceKind.Metal;
                    build.FaceThickness = 1;
                    build.Returns = ReturnKind.MetalSheet;
                    build.Back = BackKind.None;
                    build.PaintFace = true;
                    build.PaintReturns = true;
                    build.Studs = true;
                }
                else
                {
                    // Qalin akrildan kesilgan yaxlit harf, yuzi plyonka
                    build.Solid = true;
                    build.Face = FaceKind.AcrylicClear;
                    build.FaceThickness = s.Depth;
                    build.Back = BackKind.None;
                    build.OpaqueFilm = !IsNearWhite(s.FaceColor) || s.KeepLogoColors;
                }
                return build;
            }

            bool solidAcrylic = letter.Face == FaceMaterial.ClearAcrylic &&
                                letter.EnglishName.StartsWith("Solid", StringComparison.OrdinalIgnoreCase);
            if (solidAcrylic)
            {
                // Yaxlit akril harf: ichi to'la, orqasidan LED, orqa qopqoq — yupqa fomaks
                build.Solid = true;
                bool coloured = letter.EnglishName.IndexOf("colour", StringComparison.OrdinalIgnoreCase) >= 0;
                build.Face = coloured ? FaceKind.AcrylicColor : FaceKind.AcrylicOpal;
                build.FaceThickness = s.Depth;
                build.Back = choice.Back == BackKind.Auto ? BackKind.Fomax : choice.Back;
                build.Led = LedKind.Modules;
                return build;
            }

            // Ichi bo'sh hajmli harf (channel letter): yuz + bort + orqa
            switch (letter.Face)
            {
                case FaceMaterial.Acrylic:
                case FaceMaterial.ClearAcrylic:
                    build.Face = FaceKind.AcrylicOpal;
                    build.FaceThickness = letter.Bevel ? 8 : 3;
                    build.TranslucentFilm = lightFace && !letter.Has(LightZones.Dots);
                    if (letter.Has(LightZones.Dots))
                    {
                        // Ochiq LED: yuz oq plastik (fomaks), LED teshiklarga qo'yiladi
                        build.Face = FaceKind.Fomax;
                        build.FaceThickness = choice.FomaxThickness;
                        build.OpaqueFilm = lightFace;
                    }
                    break;

                case FaceMaterial.Perforated:
                    build.Face = FaceKind.Metal;
                    build.FaceThickness = 1;
                    build.PerforatedFace = true;
                    build.PaintFace = true;
                    break;

                case FaceMaterial.Pattern:
                    build.Face = FaceKind.Metal;
                    build.FaceThickness = 1;
                    build.PatternFace = true;
                    build.PaintFace = true;
                    break;

                default: // Metal
                    if (letter.Has(LightZones.Dots))
                    {
                        // Lampochkali harf: bo'yalgan metall list
                        build.Face = FaceKind.Metal;
                        build.FaceThickness = 1;
                        build.PaintFace = true;
                    }
                    else
                    {
                        // Kontrajur, yon nurli harflar: yuz — alyukobond
                        build.Face = FaceKind.Acp;
                        build.FaceThickness = choice.AcpThickness;
                    }
                    break;
            }

            if (letter.Has(LightZones.SideFull))
                build.Returns = ReturnKind.AcrylicStrip;
            else if (build.Face == FaceKind.Metal)
                build.Returns = ReturnKind.MetalSheet;
            else
                build.Returns = ReturnKind.AluminiumTape;
            if (choice.Returns != ReturnKind.Auto)
                build.Returns = choice.Returns;

            build.AcrylicBand = letter.Has(LightZones.SidePartial);
            build.Back = build.Halo ? BackKind.ClearAcrylic : BackKind.Fomax;
            if (choice.Back != BackKind.Auto)
                build.Back = choice.Back;

            build.Trim = SignDetails.SizeOf(s, Detail.Trim) > 0;
            build.PaintReturns = build.Returns == ReturnKind.MetalSheet ||
                                 (choice.PaintReturns && build.Returns != ReturnKind.AcrylicStrip);

            if (letter.Has(LightZones.Dots))
                build.Led = letter.Face == FaceMaterial.Metal ? LedKind.Bulbs : letter.Face == FaceMaterial.Perforated ? LedKind.Pixels : LedKind.OpenDots;
            else
                build.Led = LedKind.Modules;
            return build;
        }

        /// <summary>Oq rang (orakal kerak emas).</summary>
        public static bool IsNearWhite(System.Windows.Media.Color color) => color.R >= 235 && color.G >= 235 && color.B >= 235;
    }
}
