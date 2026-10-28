using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;
using ReklamaRasxod.Core;

namespace ReklamaRasxod.Estimate
{
    /// <summary>Hisob uchun kirish ma'lumotlari.</summary>
    public sealed class EstimateInput
    {
        /// <summary>Dizayn: harf va fon turi, o'lchamlar, detallar (Reklama Maket bilan bir xil).</summary>
        public SignSettings Sign { get; set; }

        /// <summary>Logo shakli. null — logo tanlanmagan (faqat fon hisoblanadi).</summary>
        public LogoShape Logo { get; set; }

        public ConstructionChoice Choice { get; set; } = new ConstructionChoice();
        public Norms Norms { get; set; } = new Norms();
    }

    /// <summary>
    /// Dizayn bo'yicha material rasxodi: logo (harflar yoki o'yilgan logo), fon, elektr, mahkamlash,
    /// kley/kraska va kesish. Har bir qatorda qayerga ketgani yoziladi. Bir xil material (masalan fomaks
    /// harf orqasiga ham, laytboksga ham) bitta qatorga yig'iladi.
    /// </summary>
    public sealed class Estimator
    {
        private static readonly double[] AcrylicThicknesses = { 2, 3, 4, 5, 6, 8, 10, 12, 15, 20, 25, 30, 40, 50 };
        private static readonly int[] ProfileWidths = { 15, 20, 25, 30, 40, 50, 60, 80, 100, 120 };

        private readonly SignSettings s;
        private readonly RuleResult rules;
        private readonly SignLayout lay;
        private readonly LogoShape logo;
        private readonly LogoMeasures m;
        private readonly ConstructionChoice c;
        private readonly Norms n;
        private readonly EstimateResult result = new EstimateResult();
        private readonly Dictionary<string, MaterialLine> lines = new Dictionary<string, MaterialLine>();
        private readonly List<MaterialLine> order = new List<MaterialLine>();
        private readonly List<string> wireParts = new List<string>();
        private double wire12;
        private bool lettersLit;

        private Estimator(EstimateInput input)
        {
            s = input.Sign ?? throw new ArgumentNullException(nameof(input), "Dizayn berilmagan.");
            c = input.Choice ?? new ConstructionChoice();
            n = input.Norms ?? new Norms();
            logo = input.Logo;
            rules = SignRules.Check(s);
            lay = LogoLayout.Compute(s);
            m = logo != null && !logo.IsEmpty ? logo.Measure(s.LogoWidth, s.LogoHeight) : new LogoMeasures();
            result.Measures = m;
        }

        public static EstimateResult Compute(EstimateInput input) => new Estimator(input).Run();

        private EstimateResult Run()
        {
            if (logo == null || logo.IsEmpty)
                result.Notes.Add("Logo tanlanmagan — harflar hisoblanmadi, faqat fon.");
            else if (logo.FillRatio > 0.92)
                result.Notes.Add("Logo deyarli to'liq to'rtburchak — orqasida fon (to'rtburchak) bilan tanlangan bo'lishi mumkin. Faqat harflarni tanlang.");

            if (m.Area > 0)
            {
                if (s.Letter.IsCut)
                    CutLogo();
                else if (!rules.CanBe3D)
                    FlatLetters();
                else
                    ChannelLetters();
            }

            Background();
            Electrics();
            Finish();
            return result;
        }

        // ================================================================ hajmli harflar

        private void ChannelLetters()
        {
            LetterStyle letter = s.Letter;
            LetterBuild b = LetterBuild.For(s, c);
            double area = m.Area;
            double perimeter = m.Perimeter;
            double depth = s.Depth;
            lettersLit = rules.LettersLit;

            if (b.Solid)
            {
                SolidSheet(b.Face, b.FaceThickness, area, "harf tanasi (yaxlit)");
                Cut(b.Face, perimeter, "harf konturi");
                result.Construction.Add("Harf: yaxlit " + FaceName(b.Face, b.FaceThickness).ToLowerInvariant());
            }
            else
            {
                Sheet(b.Face, b.FaceThickness, area, "harf yuzi");
                Cut(b.Face, perimeter, "harf yuzi");
                result.Construction.Add("Yuz: " + FaceName(b.Face, b.FaceThickness));
            }

            if (b.TranslucentFilm)
                Film(true, area, "harf yuzi");
            if (b.OpaqueFilm)
                Film(false, area, "harf yuzi");

            if (b.PatternFace)
                PatternFace(area);
            int dots = 0;
            if (b.Led == LedKind.OpenDots || b.Led == LedKind.Bulbs || b.Led == LedKind.Pixels)
                dots = FaceDots(b);

            if (!b.Solid)
            {
                double band = b.AcrylicBand ? Num.Clamp(depth * 0.25, 10, 25) : 0;
                Returns(b, Math.Max(depth - band, 5), perimeter);
                if (b.AcrylicBand)
                {
                    Sheet(SheetKind.Acrylic, AcrylicKey("opal", 3), AcrylicName("sutsimon (opal)", 3), perimeter * band / 1000,
                        "bortdagi yonadigan chiziq " + Fmt(band) + " mm");
                    Add("kesish-lazer", "Lazer kesish", "m", MaterialGroup.Processing, perimeter, "bort chizig'i");
                }

                if (b.Back != BackKind.None)
                {
                    Sheet(BackFace(b.Back), BackThickness(b.Back), area, "harf orqasi");
                    Cut(BackFace(b.Back), perimeter, "harf orqasi");
                    result.Construction.Add("Orqa: " + FaceName(BackFace(b.Back), BackThickness(b.Back)));
                }
                else
                {
                    result.Construction.Add("Orqa: ochiq");
                }

                if (b.Trim)
                    Add("kant", "Kant profili (trimcap)", "m", MaterialGroup.Profiles, perimeter * 1.05, "harf yuzi atrofi");

                double step = n[Norms.AssemblyScrewStep];
                if (step > 0 && b.Back != BackKind.None && (b.Returns == ReturnKind.AluminiumTape || b.Returns == ReturnKind.MetalSheet))
                    Add("samorez-yigish", "Samorez 3,5×9,5 (harf yig'ish)", "dona", MaterialGroup.Fasteners, perimeter * 1000 / step, "bort va orqa");

                if (b.Back != BackKind.None)
                    Glue(2 * perimeter, "harf yuzi va orqa choki");
                else
                    Glue(perimeter, "harf yuzi va bort choki");
                if (lettersLit)
                    Sealant(perimeter, "harf orqa choki (suv o'tmasin)");
            }
            else
            {
                Glue(perimeter, "harf qatlamlari");
                if (b.Back != BackKind.None && lettersLit)
                {
                    Sheet(BackFace(b.Back), 3, area, "orqa qopqoq");
                    Cut(BackFace(b.Back), perimeter, "orqa qopqoq");
                }
            }

            if (b.PaintFace)
                Paint(area, true, "harf yuzi");
            if (b.PaintReturns && !b.Solid)
                Paint(perimeter * depth / 1000, b.Returns == ReturnKind.MetalSheet, "harf borti");

            if (lettersLit)
                LetterLights(b, dots);
            else if (letter.Illuminated)
                result.Notes.Add("Harf yoritilmaydi (qoidalar: bo'yi kamida 20 sm, qalinligi kamida 4 sm) — diod va blok hisoblanmadi.");

            MountLetters(b.Studs, letter.OnStandoffs);
        }

        private void Returns(LetterBuild b, double height, double perimeter)
        {
            switch (b.Returns)
            {
                case ReturnKind.AluminiumTape:
                    Add("bort-lenta-" + Key(height), "Alyuminiy bort lentasi " + Fmt(height) + " mm", "m", MaterialGroup.Profiles,
                        perimeter * 1.05, "harf borti");
                    result.Construction.Add("Bort: alyuminiy lenta " + Fmt(height) + " mm");
                    break;

                case ReturnKind.FomaxStrip:
                    Sheet(SheetKind.Fomax, FomaxKey(c.FomaxThickness), FomaxName(c.FomaxThickness), perimeter * height / 1000, "harf borti (tasma)");
                    Add("kesish-frezer", "Frezer kesish (CNC)", "m", MaterialGroup.Processing, perimeter, "bort tasmasi");
                    result.Construction.Add("Bort: fomaks tasma " + Fmt(c.FomaxThickness) + " mm");
                    break;

                case ReturnKind.AcrylicStrip:
                    Sheet(SheetKind.Acrylic, AcrylicKey("opal", 3), AcrylicName("sutsimon (opal)", 3), perimeter * height / 1000, "harf borti (yonadi)");
                    Add("kesish-lazer", "Lazer kesish", "m", MaterialGroup.Processing, perimeter, "bort tasmasi");
                    result.Construction.Add("Bort: sutsimon akril 3 mm (yonadi)");
                    break;

                case ReturnKind.AcpStrip:
                    Sheet(SheetKind.Acp, AcpKey(), AcpName(), perimeter * height / 1000, "harf borti (tasma)");
                    Add("kesish-frezer", "Frezer kesish (CNC)", "m", MaterialGroup.Processing, perimeter, "bort tasmasi");
                    result.Construction.Add("Bort: alyukobond tasma");
                    break;

                case ReturnKind.MetalSheet:
                    Sheet(SheetKind.Metal, MetalKey(1), MetalName(1), perimeter * height / 1000, "harf borti");
                    result.Construction.Add("Bort: metall list 1 mm");
                    break;
            }
        }

        private void LetterLights(LetterBuild b, int dots)
        {
            LetterStyle letter = s.Letter;
            switch (b.Led)
            {
                case LedKind.Modules:
                {
                    int count = ModuleCount(m.Area, m.Perimeter, s.Depth);
                    if (b.Halo && letter.Has(LightZones.Face))
                        count = (int)Math.Ceiling(count * 1.25); // yuz ham, orqa (kontrajur) ham yonadi
                    Leds(count, "harflar ichida, qadam " + Fmt(ModuleStep(s.Depth)) + " mm");
                    result.Construction.Add("Yoritish: LED modullar");
                    break;
                }

                case LedKind.OpenDots:
                    Add("ochiq-led", "Ochiq LED (nuqta) 12V", "dona", MaterialGroup.Electric, dots, "harf yuzida");
                    AddWatts(dots * n[Norms.DotPower]);
                    result.Construction.Add("Yoritish: ochiq LED nuqtalar");
                    break;

                case LedKind.Pixels:
                    Add("piksel-led", "Piksel LED 12V", "dona", MaterialGroup.Electric, dots, "teshiklarda");
                    AddWatts(dots * n[Norms.DotPower]);
                    result.Construction.Add("Yoritish: piksel LED");
                    break;

                case LedKind.Bulbs:
                    Add("lampochka", "Lampochka (LED, 220V)", "dona", MaterialGroup.Electric, dots, "harf yuzida");
                    Add("patron", "Patron (lampochka uchun)", "dona", MaterialGroup.Electric, dots, "harf yuzida");
                    result.Watts220 += dots * n[Norms.BulbPower];
                    wire12 += dots * SignDetails.DotSpacing(s) / 1000 * 1.2;
                    wireParts.Add("lampochkalar orasi");
                    result.Construction.Add("Yoritish: lampochkalar (220V)");
                    break;
            }

            if (b.RimStrip)
            {
                double strip = m.Perimeter * 1.05;
                Add("neon-lenta", "Neon lenta (flex) 12V", "m", MaterialGroup.Electric, strip, "harf cheti");
                AddWatts(strip * n[Norms.StripPower]);
            }

            if (b.Led != LedKind.Bulbs)
            {
                wire12 += m.Elements * n[Norms.WirePerLetter] + s.LogoWidth / 1000;
                wireParts.Add("harflarni ulash");
            }
        }

        /// <summary>Yuzdagi nuqtalar (ochiq LED, lampochka, perforatsiya) — Corel chizmasidagi to'r bilan bir xil.</summary>
        private int FaceDots(LetterBuild b)
        {
            if (logo == null)
                return 0;
            double diameter = SignDetails.SizeOf(s, Detail.Dots);
            double trim = SignDetails.SizeOf(s, Detail.Trim);
            bool perforated = b.Led == LedKind.Pixels;
            double clearance = diameter / 2 + (perforated ? trim * 1.5 : trim);
            int count = logo.CountInsidePoints(s.LogoWidth, s.LogoHeight, SignDetails.DotSpacing(s), clearance, perforated);
            if (perforated)
                Add("kesish-lazer", "Lazer kesish", "m", MaterialGroup.Processing, count * Math.PI * diameter / 1000,
                    "perforatsiya (" + count + " ta teshik Ø" + Fmt(diameter) + ")");
            return count;
        }

        /// <summary>Naqshli lazer kesma yuz: naqsh teshiklari va orqasidagi akril qatlam.</summary>
        private void PatternFace(double area)
        {
            Sheet(SheetKind.Acrylic, AcrylicKey("opal", 3), AcrylicName("sutsimon (opal)", 3), area, "naqsh orqasidagi akril");
            Add("kesish-lazer", "Lazer kesish", "m", MaterialGroup.Processing, m.Perimeter, "naqsh orqasidagi akril");
            if (logo == null)
                return;
            double side = SignDetails.SizeOf(s, Detail.FacePattern);
            int count = logo.CountInsidePoints(s.LogoWidth, s.LogoHeight, SignDetails.FacePatternSpacing(s), side * 0.75, true);
            Add("kesish-lazer", "Lazer kesish", "m", MaterialGroup.Processing, count * 4 * side / 1000, "yuzdagi naqsh (" + count + " ta element)");
        }

        // ================================================================ yassi harf va o'yilgan logo

        private void FlatLetters()
        {
            LetterStyle letter = s.Letter;
            result.Notes.Add("Logo bo'yi 20 sm dan past — yassi harf (hajmsiz, yoritilmaydi) deb hisoblandi.");

            SheetKind kind;
            double thickness;
            string key, name;
            if (letter.Face == FaceMaterial.Metal)
            {
                kind = SheetKind.Acp;
                thickness = c.AcpThickness;
                key = AcpKey();
                name = AcpName();
            }
            else if (letter.Face == FaceMaterial.ClearAcrylic && !letter.Illuminated)
            {
                kind = SheetKind.Acrylic;
                thickness = StandardAcrylic(Num.Clamp(s.Depth, 3, 10));
                key = AcrylicKey("shaffof", thickness);
                name = AcrylicName("shaffof", thickness);
            }
            else
            {
                kind = SheetKind.Fomax;
                thickness = c.FomaxThickness;
                key = FomaxKey(thickness);
                name = FomaxName(thickness);
            }

            Sheet(kind, key, name, m.Area, "yassi harflar");
            Add(kind == SheetKind.Acrylic ? "kesish-lazer" : "kesish-frezer", kind == SheetKind.Acrylic ? "Lazer kesish" : "Frezer kesish (CNC)", "m",
                MaterialGroup.Processing, m.Perimeter, "harf konturi");
            if (!LetterBuild.IsNearWhite(s.FaceColor) || s.KeepLogoColors)
                Film(false, m.Area, "harf yuzi");
            Add("skotch", "Ikki tomonlama skotch (19 mm)", "m", MaterialGroup.Chemicals, Math.Max(m.Area * 0.2 / 0.019, 0.5), "harflarni yopishtirish");
            result.Construction.Add("Harf: yassi " + name.ToLowerInvariant());
        }

        private void CutLogo()
        {
            LetterStyle letter = s.Letter;
            CutMode mode = letter.Cut == CutMode.PushThrough && !rules.CanBe3D ? CutMode.RoutedLit : letter.Cut;
            double area = m.Area;
            double perimeter = m.Perimeter;
            lettersLit = rules.LettersLit;
            bool colourFilm = !LetterBuild.IsNearWhite(s.FaceColor) || s.KeepLogoColors;

            if (mode == CutMode.Engraved)
            {
                Add("gravirovka", "Gravirovka (frezer, V-o'yiq)", "m", MaterialGroup.Processing, perimeter, "logo konturi");
                result.Construction.Add("Logo: alyukobond yuzga gravirovka");
                return;
            }

            Add("kesish-frezer", "Frezer kesish (CNC)", "m", MaterialGroup.Processing, perimeter, "fon yuzidan logoni o'yish");
            switch (mode)
            {
                case CutMode.RoutedLit:
                {
                    double t = StandardAcrylic(s.Depth);
                    SolidSheet(FaceKind.AcrylicOpal, t, area, "o'yiqqa qo'yiladigan akril");
                    Add("kesish-lazer", "Lazer kesish", "m", MaterialGroup.Processing, perimeter, "akril qo'yma");
                    if (colourFilm)
                        Film(true, area, "akril yuzi");
                    Glue(perimeter, "akrilni o'yiqqa yopishtirish");
                    result.Construction.Add("Logo: o'yiq + sutsimon akril " + Fmt(t) + " mm");
                    break;
                }

                case CutMode.PushThrough:
                {
                    double t = StandardAcrylic(s.Depth);
                    SolidSheet(FaceKind.AcrylicClear, t, area, "chiqib turuvchi akril harf");
                    Add("kesish-lazer", "Lazer kesish", "m", MaterialGroup.Processing, perimeter, "akril harf");
                    if (colourFilm)
                        Film(true, area, "harf yuzi");
                    Glue(perimeter, "akril harfni o'rnatish");
                    result.Construction.Add("Logo: push-through, shaffof akril " + Fmt(t) + " mm");
                    break;
                }

                case CutMode.RoutedBacking:
                {
                    double width = s.LogoWidth + 60;
                    double height = s.LogoHeight + 60;
                    Sheet(SheetKind.Acp, AcpKey(), AcpName(), width * height / 1e6, "o'yiq orqasidagi rangli panel");
                    Add("kesish-frezer", "Frezer kesish (CNC)", "m", MaterialGroup.Processing, 2 * (width + height) / 1000, "orqa panel");
                    Add("samorez-metall", "Samorez 4,2×25 (metall / panelga)", "dona", MaterialGroup.Fasteners,
                        2 * (width + height) / n[Norms.PanelScrewStep], "orqa panel");
                    result.Construction.Add("Logo: o'yiq + orqasida rangli alyukobond");
                    break;
                }
            }

            if (lettersLit && (mode == CutMode.RoutedLit || mode == CutMode.PushThrough))
            {
                double boxDepth = Math.Max(s.BackgroundDepth, SignRules.MinLitDepthMm);
                Leds(ModuleCount(area, perimeter, boxDepth), "quti ichida, logo orqasida");
                wire12 += n[Norms.WirePerLetter] * Math.Max(1, m.Elements / 3.0) + s.LogoWidth / 1000;
                wireParts.Add("logo orqasidagi modullar");
                result.Construction.Add("Yoritish: LED modullar (quti ichida)");
            }
        }

        // ================================================================ fon

        private void Background()
        {
            BackgroundStyle background = s.Background;
            double bw = lay.BackgroundWidth;
            double bh = lay.BackgroundHeight;
            double bd = s.BackgroundDepth;
            double area = bw * bh / 1e6;
            double perimeter = 2 * (bw + bh) / 1000;
            result.Construction.Add(background.Kind == BackgroundKind.Wall
                ? "Fon: yo'q — logo devorga"
                : "Fon: " + background.Name + ", " + Fmt(Math.Round(bw)) + " × " + Fmt(Math.Round(bh)) + " mm");

            switch (background.Kind)
            {
                case BackgroundKind.Wall:
                    break;

                case BackgroundKind.Panel:
                    Sheet(SheetKind.Acp, AcpKey(), AcpName(), area, "fon paneli");
                    Add("kesish-frezer", "Frezer kesish (CNC)", "m", MaterialGroup.Processing, perimeter, "fon paneli");
                    PanelOnFrame(bw, bh, "fon karkasi");
                    break;

                case BackgroundKind.Cassette:
                {
                    double fold = Math.Max(bd, 20);
                    Sheet(SheetKind.Acp, AcpKey(), AcpName(), (bw + 2 * fold) * (bh + 2 * fold) / 1e6, "kasseta (bukiladigan chetlari bilan)");
                    Add("kesish-frezer", "Frezer kesish (CNC)", "m", MaterialGroup.Processing, 2 * perimeter, "kasseta: kesish va bukish chizig'i");
                    Add("zaklepka", "Zaklepka (parchin mix) 4×10", "dona", MaterialGroup.Fasteners, 4 * Math.Max(2, Math.Ceiling(fold / 30)), "kasseta burchaklari");
                    PanelOnFrame(bw, bh, "kasseta karkasi");
                    break;
                }

                case BackgroundKind.Lightbox:
                    Lightbox(bw, bh, bd);
                    break;

                case BackgroundKind.AcrylicPlate:
                {
                    double t = StandardAcrylic(Math.Max(bd, 3));
                    Sheet(SheetKind.Acrylic, AcrylicKey("shaffof", t), AcrylicName("shaffof", t), area, "akril plita");
                    Add("kesish-lazer", "Lazer kesish", "m", MaterialGroup.Processing, perimeter, "akril plita");
                    double diameter = Math.Round(SignDetails.SizeOf(s, Detail.Bolts));
                    double bolts = Math.Max(4, Math.Round(perimeter / 0.6));
                    Add("dekor-bolt-" + Key(diameter), "Dekor distansiya bolt Ø" + Fmt(diameter) + " mm", "dona", MaterialGroup.Fasteners, bolts, "akril plitani ushlab turadi");
                    Add("dyubel", "Dyubel-samorez 6×40 (devorga)", "dona", MaterialGroup.Fasteners, bolts, "distansiya boltlari");
                    break;
                }

                case BackgroundKind.AcpBox:
                {
                    Sheet(SheetKind.Acp, AcpKey(), AcpName(), area, "quti yuzi");
                    Sheet(SheetKind.Acp, AcpKey(), AcpName(), perimeter * bd / 1000, "quti bortlari");
                    Sheet(SheetKind.Acp, AcpKey(), AcpName(), area, "quti orqasi");
                    Add("kesish-frezer", "Frezer kesish (CNC)", "m", MaterialGroup.Processing, 2 * perimeter, "quti yuzi va bortlari");
                    double bars = FrameBars(bw) * bh / 1000;
                    double frame = 2 * perimeter + 4 * bd / 1000 + bars;
                    Profile(frame, "quti karkasi (old va orqa ramka, ustunlar)");
                    Add("samorez-metall", "Samorez 4,2×25 (metall / panelga)", "dona", MaterialGroup.Fasteners,
                        (2 * perimeter + bars) * 1000 / n[Norms.PanelScrewStep], "alyukobondni karkasga");
                    Add("zaklepka", "Zaklepka (parchin mix) 4×10", "dona", MaterialGroup.Fasteners, 8 * Math.Max(2, Math.Ceiling(bd / 40)), "quti burchaklari");
                    Sealant(2 * perimeter, "quti choklari");
                    Anchors(perimeter, "quti devorga");
                    break;
                }

                case BackgroundKind.PatternSheet:
                case BackgroundKind.FramedMesh:
                case BackgroundKind.WoodFrameMesh:
                    PatternBackground(bw, bh, bd);
                    break;

                case BackgroundKind.HorizontalSlats:
                case BackgroundKind.VerticalSlats:
                    Slats(bw, bh);
                    break;
            }

            if (s.EngravedBorder && background.CanHaveBorder)
            {
                double inset = SignDetails.SizeOf(s, Detail.BorderInset);
                double length = 2 * (Math.Max(bw - 2 * inset, 0) + Math.Max(bh - 2 * inset, 0)) / 1000;
                Add("gravirovka", "Gravirovka (frezer, V-o'yiq)", "m", MaterialGroup.Processing, length, "fon chetidagi ramka chizig'i");
            }
        }

        /// <summary>Tekis panel yoki kasseta: profil karkas, samorez, dyubel, kraska.</summary>
        private void PanelOnFrame(double bw, double bh, string part)
        {
            double perimeter = 2 * (bw + bh) / 1000;
            double frame = perimeter + FrameBars(bw) * bh / 1000;
            Profile(frame, part);
            Add("samorez-metall", "Samorez 4,2×25 (metall / panelga)", "dona", MaterialGroup.Fasteners, frame * 1000 / n[Norms.PanelScrewStep], "panelni karkasga");
            Anchors(perimeter, "karkasni devorga");
        }

        private void Lightbox(double bw, double bh, double bd)
        {
            double area = bw * bh / 1e6;
            double perimeter = 2 * (bw + bh) / 1000;
            bool cut = s.Letter.IsCut;
            if (cut)
                Sheet(SheetKind.Acp, AcpKey(), AcpName(), area, "laytboks yuzi (logo o'yiladi)");
            else
                Sheet(SheetKind.Acrylic, AcrylicKey("opal", 3), AcrylicName("sutsimon (opal)", 3), area, "laytboks yuzi");
            Add(cut ? "kesish-frezer" : "kesish-lazer", cut ? "Frezer kesish (CNC)" : "Lazer kesish", "m", MaterialGroup.Processing, perimeter, "laytboks yuzi");

            double profile = Math.Round(SignDetails.SizeOf(s, Detail.Frame));
            if (profile > 0)
                Add("laytboks-profil-" + Key(profile), "Laytboks profili " + Fmt(profile) + " mm (alyuminiy)", "m", MaterialGroup.Profiles, perimeter * 1.05, "quti ramkasi");
            else
                Sheet(SheetKind.Acp, AcpKey(), AcpName(), perimeter * bd / 1000, "quti bortlari");
            Sheet(SheetKind.Acp, AcpKey(), AcpName(), area, "quti orqasi");

            double bars = Math.Max(0, Math.Ceiling(bw / 1000) - 1);
            if (bars > 0)
                Profile(bars * bh / 1000, "laytboks ichki ustunlari");

            if (rules.BackgroundLit && !cut)
            {
                int count = GridModules(new Rect(0, 0, bw, bh), bd, point => true);
                Leds(count, "laytboks ichida, qadam " + Fmt(BackgroundStep(bd)) + " mm");
                wire12 += perimeter * 0.5;
                wireParts.Add("laytboks ichida");
            }

            Add("samorez-metall", "Samorez 4,2×25 (metall / panelga)", "dona", MaterialGroup.Fasteners, 2 * perimeter * 1000 / n[Norms.PanelScrewStep], "profilga yuz va orqa");
            Sealant(2 * perimeter, "quti choklari");
            Anchors(perimeter, "laytboksni devorga");
        }

        /// <summary>Gulli list, ramkadagi setka, yog'och ramkali setka.</summary>
        private void PatternBackground(double bw, double bh, double bd)
        {
            BackgroundStyle background = s.Background;
            double area = bw * bh / 1e6;
            double perimeter = 2 * (bw + bh) / 1000;
            double frameWidth = SignDetails.SizeOf(s, Detail.Frame);
            var inner = new Rect(frameWidth, frameWidth, Math.Max(bw - 2 * frameWidth, 1), Math.Max(bh - 2 * frameWidth, 1));
            double innerArea = inner.Width * inner.Height / 1e6;
            PatternStyle pattern = SignDetails.Pattern(s);

            if (background.Kind == BackgroundKind.PatternSheet)
            {
                double edge = Num.Clamp(Math.Min(inner.Width, inner.Height) * 0.03, 15, 40);
                var holeArea = new Rect(inner.X + edge, inner.Y + edge, Math.Max(inner.Width - 2 * edge, 1), Math.Max(inner.Height - 2 * edge, 1));
                List<Hole> holes = PatternGeometry.Holes(pattern, holeArea, out double scale);
                double holesLength = holes.Sum(hole => PolygonLength(hole.Points)) * scale / 1000;
                int holeCount = (int)Math.Round(holes.Count * scale * scale);
                double open = OpenFraction(holes, holeArea);

                Sheet(SheetKind.Metal, MetalKey(1.5), MetalName(1.5), area, "gulli list");
                Add("kesish-lazer", "Lazer kesish", "m", MaterialGroup.Processing, holesLength + perimeter,
                    "gul teshiklari (~" + holeCount + " ta, " + pattern.Name.ToLowerInvariant() + ")");
                Paint(area * 2 * (1 - open), true, "gulli list (ikki tomoni)");
                if (frameWidth > 0)
                    Profile(perimeter, "list ramkasi", ProfileFor(frameWidth));
                PanelOnFrame(bw, bh, "list karkasi");
            }
            else
            {
                double open = OpenFraction(PatternGeometry.Holes(pattern, new Rect(0, 0, pattern.Pitch * 20, pattern.Pitch * 20), out _),
                    new Rect(0, 0, pattern.Pitch * 20, pattern.Pitch * 20));
                Sheet(SheetKind.None, "setka-" + Key(pattern.Pitch) + "-" + pattern.Shape, "Setka: " + pattern.Name.ToLowerInvariant(),
                    innerArea, "ramka ichidagi setka");
                Paint(innerArea * 2 * (1 - open), true, "setka (ikki tomoni)");

                if (background.Kind == BackgroundKind.FramedMesh)
                {
                    double bars = inner.Width > 1500 ? Math.Round(inner.Width / 1000) - 1 : 0;
                    Profile(perimeter + bars * inner.Height / 1000, "setka ramkasi", ProfileFor(frameWidth));
                    Add("samorez-metall", "Samorez 4,2×25 (metall / panelga)", "dona", MaterialGroup.Fasteners,
                        2 * (inner.Width + inner.Height) / n[Norms.PanelScrewStep], "setkani ramkaga");
                    Anchors(perimeter, "ramkani devorga");
                }
                else
                {
                    double wood = Math.Max(Math.Round(frameWidth), 20);
                    Add("yogoch-" + Key(wood), "Yog'och brus " + Fmt(wood) + "×40 mm", "m", MaterialGroup.Profiles, perimeter * 1.1, "ramka");
                    Add("lak", "Lak / morilka (yog'och)", "kg", MaterialGroup.Chemicals, perimeter * 2 * (wood + 40) / 1000 * 0.1, "yog'och ramka");
                    Add("samorez-yogoch", "Samorez 3,5×35 (yog'ochga)", "dona", MaterialGroup.Fasteners,
                        2 * (inner.Width + inner.Height) / n[Norms.PanelScrewStep] + 8, "setka va ramka burchaklari");
                    Anchors(perimeter, "ramkani devorga");
                }
            }

            if (!rules.BackgroundLit)
                return;

            // Orqasi yonadi: quti (bort + orqa devor), nur tarqatgich va LED
            if (background.Kind != BackgroundKind.WoodFrameMesh)
                Sheet(SheetKind.Acp, AcpKey(), AcpName(), perimeter * bd / 1000, "orqa quti bortlari");
            Sheet(SheetKind.Acp, AcpKey(), AcpName(), area, "orqa quti devori");
            Sheet(SheetKind.Acrylic, AcrylicKey("opal", 3), AcrylicName("sutsimon (opal)", 3), innerArea, "nur tarqatgich (diffuzor)");
            Sealant(perimeter, "orqa quti choki");
            BackgroundLeds(inner, bd);
        }

        private void Slats(double bw, double bh)
        {
            bool horizontal = s.Background.Kind == BackgroundKind.HorizontalSlats;
            var full = new Rect(0, 0, bw, bh);
            List<Rect> slats = PatternGeometry.Slats(full, horizontal, SignDetails.SizeOf(s, Detail.SlatWidth), SignDetails.SizeOf(s, Detail.SlatGap));
            double length = horizontal ? bw : bh;
            double slatWidth = horizontal ? slats[0].Height : slats[0].Width;
            int[] slatProfile = ProfileFor(slatWidth);
            Profile(slats.Count * length / 1000, "reykalar (" + slats.Count + " ta × " + Fmt(Math.Round(length)) + " mm)", slatProfile, false);

            int supports;
            double supportLength;
            if (horizontal)
            {
                supports = Math.Max(2, (int)Math.Round(bw / 1200) + 1);
                supportLength = bh;
                Profile(supports * bh / 1000, "kronshteyn ustunlar (" + supports + " ta)", new[] { 40, 20 }, false);
            }
            else
            {
                supports = bh > 1200 ? 3 : 2;
                supportLength = bw;
                Profile(supports * bw / 1000, "ko'ndalang reykalar (" + supports + " ta)", null, false);
            }
            Add("samorez-metall", "Samorez 4,2×25 (metall / panelga)", "dona", MaterialGroup.Fasteners, slats.Count * supports * 2, "reykani ustunga");
            Add("dyubel", "Dyubel-samorez 6×40 (devorga)", "dona", MaterialGroup.Fasteners,
                supports * Math.Max(2, Math.Ceiling(supportLength / n[Norms.AnchorStep])), "ustunlarni devorga");

            if (!rules.BackgroundLit)
                return;

            double strip = 0;
            foreach (Rect gap in PatternGeometry.SlatGaps(slats, horizontal, 16))
            {
                var center = new Point(gap.X + gap.Width / 2, gap.Y + gap.Height / 2);
                if (PatternGeometry.Level(s.BackgroundLight, center, full, lay.Logo) > 0)
                    strip += (horizontal ? gap.Width : gap.Height) / 1000;
            }
            LedStrip(strip, "reykalar orasida");
        }

        /// <summary>Gulli / setkali fon orqasidagi nur: qaysi joyi yonishiga qarab lenta yoki modullar.</summary>
        private void BackgroundLeds(Rect inner, double depth)
        {
            var full = new Rect(0, 0, lay.BackgroundWidth, lay.BackgroundHeight);
            switch (s.BackgroundLight)
            {
                case BackgroundLight.Perimeter:
                    LedStrip(2 * (inner.Width + inner.Height) / 1000, "fon chetlari bo'ylab");
                    break;

                case BackgroundLight.Bottom:
                    LedStrip(2 * inner.Width / 1000, "fon pastida (2 qator)");
                    break;

                default:
                {
                    int count = GridModules(inner, depth, point => PatternGeometry.Level(s.BackgroundLight, point, full, lay.Logo) > 0);
                    Leds(count, s.BackgroundLight == BackgroundLight.AroundLogo ? "fon orqasida, logo atrofi" : "fon orqasida");
                    break;
                }
            }
            wire12 += (inner.Width + inner.Height) / 1000;
            wireParts.Add("fon orqasida");
        }

        // ================================================================ elektr va mahkamlash

        private void Electrics()
        {
            double watts = result.Watts12;
            if (watts > 0)
            {
                double psu = n[Norms.PsuPower];
                double reserve = n[Norms.PsuReserve];
                int count = (int)Math.Ceiling(watts * (1 + reserve / 100) / psu - 1e-9);
                Add("blok-" + Key(psu), "Blok pitaniya 12V " + Fmt(psu) + " W", "dona", MaterialGroup.Electric, count,
                    "jami " + Fmt(Math.Round(watts, 1)) + " W + " + Fmt(reserve) + "% zaxira");
                Add("germetik-quti", "Germetik quti (blok uchun)", "dona", MaterialGroup.Electric, count, "blok pitaniya");
                wire12 += n[Norms.WireToPsu];
                wireParts.Add("blokgacha");
                Add("klemma", "Klemma (Wago)", "dona", MaterialGroup.Electric, 2 * (lettersLit ? Math.Max(m.Elements, 1) : 1) + 2 * count, "ulanishlar");
            }

            if (wire12 > 0)
                Add("sim-2x075", "Sim 2×0,75 (ShVVP)", "m", MaterialGroup.Electric, wire12, string.Join(", ", wireParts.Distinct()));

            if (watts > 0 || result.Watts220 > 0)
            {
                double mains = n[Norms.MainsCable];
                Add("kabel-3x15", "Kabel 3×1,5 (220V)", "m", MaterialGroup.Electric, mains, "tarmoqqacha");
                Add("gofra-16", "Gofra truba 16 mm", "m", MaterialGroup.Electric, mains + n[Norms.WireToPsu], "sim va kabel uchun");
            }
        }

        private void MountLetters(bool studs, bool standoffs)
        {
            double fix = n[Norms.FixArea];
            double points = 0;
            foreach (double element in m.ElementAreas)
                points += element < 0.004 ? 1 : Math.Max(2, Math.Ceiling(element / fix));
            if (points == 0)
                points = Math.Max(2, Math.Ceiling(m.Area / fix));

            if (studs)
            {
                Add("shpilka", "Shpilka M6 (metall harf uchun)", "dona", MaterialGroup.Fasteners, points, "harflarni mahkamlash");
                Add("epoksid", "Epoksid kley", "dona", MaterialGroup.Chemicals, Math.Ceiling(points / 30), "shpilkalar");
                return;
            }

            if (standoffs && rules.CanBe3D)
            {
                double length = Math.Round(SignDetails.SizeOf(s, Detail.Standoff));
                if (length > 0)
                    Add("distansiya-" + Key(length), "Distansiya (vtulka) " + Fmt(length) + " mm", "dona", MaterialGroup.Fasteners, points, "harfni ko'tarib turadi");
            }

            if (s.Background.Kind == BackgroundKind.Wall)
                Add("dyubel", "Dyubel-samorez 6×40 (devorga)", "dona", MaterialGroup.Fasteners, points, "harflarni devorga");
            else
                Add("samorez-metall", "Samorez 4,2×25 (metall / panelga)", "dona", MaterialGroup.Fasteners, points, "harflarni fonga");
        }

        private void Anchors(double perimeter, string part)
        {
            Add("dyubel", "Dyubel-samorez 6×40 (devorga)", "dona", MaterialGroup.Fasteners,
                Math.Max(4, Math.Ceiling(perimeter * 1000 / n[Norms.AnchorStep])), part);
        }

        // ================================================================ hisob yordamchilari

        /// <summary>Harf ichidagi modullar qadami, mm.</summary>
        private double ModuleStep(double depth) => Num.Clamp(depth * n[Norms.LedStep], 50, 250);

        /// <summary>Fon / laytboks orqasidagi modullar qadami, mm (nur tarqatgich bor — siyrakroq).</summary>
        private double BackgroundStep(double depth) => Num.Clamp(depth * n[Norms.LedStep], 100, 250);

        /// <summary>
        /// LED modullar soni: modullar har bir chiziq (shtrix) o'rta chizig'i bo'ylab qadam bilan qo'yiladi,
        /// keng joyda bir necha qator. Qadam = qalinlik × norma (50–250 mm). Har bir bo'lakka kamida bitta.
        /// </summary>
        private int ModuleCount(double area, double perimeter, double depth)
        {
            if (area <= 0 || perimeter <= 0)
                return 0;
            double step = ModuleStep(depth);
            double stroke = 2 * area / perimeter * 1000;
            int rows = Math.Max(1, (int)Math.Round(stroke / step));
            double count = perimeter * 1000 / 2 / step * rows;
            return Math.Max((int)Math.Ceiling(count), Math.Max(m.Elements, 1));
        }

        /// <summary>Yuza bo'yicha to'r: har qadamda bitta modul.</summary>
        private int GridModules(Rect area, double depth, Func<Point, bool> include)
        {
            double step = BackgroundStep(depth);
            int count = 0;
            for (double y = area.Top + step / 2; y < area.Bottom; y += step)
            {
                for (double x = area.Left + step / 2; x < area.Right; x += step)
                {
                    if (include(new Point(x, y)))
                        count++;
                }
            }
            return count;
        }

        private void Leds(int count, string part)
        {
            if (count <= 0)
                return;
            double power = n[Norms.LedPower];
            Add("led-modul", "LED modul 12V (" + Fmt(power) + " W)", "dona", MaterialGroup.Electric, count, part);
            AddWatts(count * power);
        }

        private void LedStrip(double meters, string part)
        {
            if (meters <= 0)
                return;
            meters *= 1.05;
            Add("led-lenta", "LED lenta 12V", "m", MaterialGroup.Electric, meters, part);
            AddWatts(meters * n[Norms.StripPower]);
        }

        private void AddWatts(double watts) => result.Watts12 += watts;

        private void Glue(double seam, string part)
        {
            if (seam > 0)
                AddRaw("kley", "Kley (Kosmofen, 200 g flakon)", "dona", MaterialGroup.Chemicals, seam / n[Norms.GlueRate], part + " " + Fmt(seam) + " m");
        }

        private void Sealant(double seam, string part)
        {
            if (seam > 0)
                AddRaw("germetik", "Germetik (silikon, 280 ml)", "dona", MaterialGroup.Chemicals, seam / n[Norms.SealantRate], part + " " + Fmt(seam) + " m");
        }

        /// <summary>Bo'yaladigan yuza: kraska (necha qavat) va metall uchun gruntovka.</summary>
        private void Paint(double area, bool metal, string part)
        {
            if (area <= 0)
                return;
            string text = part + " " + Fmt(area) + " m²";
            AddRaw("kraska", "Kraska (emal)", "kg", MaterialGroup.Chemicals, area * n[Norms.PaintRate] * n[Norms.PaintCoats], text);
            if (metal && n[Norms.PrimerRate] > 0)
                AddRaw("grunt", "Gruntovka (metall uchun)", "kg", MaterialGroup.Chemicals, area * n[Norms.PrimerRate], text);
        }

        private void Film(bool translucent, double area, string part)
        {
            double waste = n[Norms.FilmWaste] / 100;
            string key = translucent ? "orakal-translucent" : "orakal-oddiy";
            string name = translucent ? "Orakal translucent (8500, nur o'tkazadi)" : "Orakal oddiy (641)";
            MaterialLine line = Line(key, name, "m²", MaterialGroup.Sheets);
            line.Quantity += area * (1 + waste);
            line.AddPart(part + " " + Fmt(area) + " m²");
            line.SheetWidth = n[Norms.FilmRoll];
        }

        /// <summary>Profil quvur (karkas): metr, kraska, gruntovka va (payvandlansa) elektrod.</summary>
        private void Profile(double meters, string part, int[] size = null, bool welded = true)
        {
            if (meters <= 0)
                return;
            int a = size != null ? size[0] : c.ProfileA;
            int b = size != null ? size[1] : c.ProfileB;
            MaterialLine line = Line("profil-" + a + "x" + b, "Profil quvur " + a + "×" + b + " mm", "m", MaterialGroup.Profiles);
            line.Quantity += meters * 1.05;
            line.AddPart(part + " " + Fmt(meters) + " m");
            line.PieceLength = n[Norms.TubeLength];
            Paint(meters * 2 * (a + b) / 1000, true, "profil " + a + "×" + b);
            if (welded)
                AddRaw("elektrod", "Elektrod (payvandlash) 3 mm", "kg", MaterialGroup.Profiles, meters * 0.02, "karkas " + Fmt(meters) + " m");
        }

        private int[] ProfileFor(double width)
        {
            int a = ProfileWidths.FirstOrDefault(w => w >= width - 0.5);
            if (a == 0)
                a = ProfileWidths[ProfileWidths.Length - 1];
            return new[] { a, a <= 20 ? a : Math.Max(20, a / 2) };
        }

        private double FrameBars(double width) => Math.Max(0, Math.Ceiling(width / n[Norms.FrameStep]) - 1);

        // ================================================================ listlar

        private void Sheet(FaceKind face, double thickness, double area, string part)
        {
            switch (face)
            {
                case FaceKind.AcrylicOpal:
                    Sheet(SheetKind.Acrylic, AcrylicKey("opal", thickness), AcrylicName("sutsimon (opal)", thickness), area, part);
                    break;
                case FaceKind.AcrylicColor:
                    Sheet(SheetKind.Acrylic, AcrylicKey("rangli", thickness), AcrylicName("rangli", thickness), area, part);
                    break;
                case FaceKind.AcrylicClear:
                    Sheet(SheetKind.Acrylic, AcrylicKey("shaffof", thickness), AcrylicName("shaffof", thickness), area, part);
                    break;
                case FaceKind.Acp:
                    Sheet(SheetKind.Acp, AcpKey(), AcpName(), area, part);
                    break;
                case FaceKind.Metal:
                    Sheet(SheetKind.Metal, MetalKey(thickness), MetalName(thickness), area, part);
                    break;
                default:
                    Sheet(SheetKind.Fomax, FomaxKey(thickness), FomaxName(thickness), area, part);
                    break;
            }
        }

        /// <summary>Yaxlit (qalin) akril: 50 mm dan qalin bo'lsa, bir necha qatlam yopishtiriladi.</summary>
        private void SolidSheet(FaceKind face, double thickness, double area, string part)
        {
            if (face == FaceKind.Fomax || face == FaceKind.Acp || face == FaceKind.Metal || thickness <= 50)
            {
                Sheet(face, face == FaceKind.Fomax || face == FaceKind.Acp || face == FaceKind.Metal ? thickness : StandardAcrylic(thickness), area, part);
                return;
            }
            int layers = (int)Math.Ceiling(thickness / 50);
            double layer = StandardAcrylic(thickness / layers);
            Sheet(face, layer, area * layers, part + ", " + layers + " qatlam");
        }

        private void Sheet(SheetKind kind, string key, string name, double area, string part)
        {
            if (area <= 0)
                return;
            MaterialLine line = Line(key, name, "m²", MaterialGroup.Sheets);
            line.Sheet = kind;
            line.Quantity += area * (1 + n[Norms.SheetWaste] / 100);
            line.AddPart(part + " " + Fmt(area) + " m²");
            switch (kind)
            {
                case SheetKind.Acrylic:
                    line.SheetWidth = n[Norms.AcrylicSheetW];
                    line.SheetHeight = n[Norms.AcrylicSheetH];
                    break;
                case SheetKind.Fomax:
                    line.SheetWidth = n[Norms.FomaxSheetW];
                    line.SheetHeight = n[Norms.FomaxSheetH];
                    break;
                case SheetKind.Acp:
                    line.SheetWidth = n[Norms.AcpSheetW];
                    line.SheetHeight = n[Norms.AcpSheetH];
                    break;
                case SheetKind.Metal:
                    line.SheetWidth = n[Norms.MetalSheetW];
                    line.SheetHeight = n[Norms.MetalSheetH];
                    break;
            }
        }

        private void Cut(FaceKind face, double length, string part)
        {
            bool laser = face == FaceKind.AcrylicOpal || face == FaceKind.AcrylicColor || face == FaceKind.AcrylicClear || face == FaceKind.Metal;
            Add(laser ? "kesish-lazer" : "kesish-frezer", laser ? "Lazer kesish" : "Frezer kesish (CNC)", "m", MaterialGroup.Processing, length, part);
        }

        private FaceKind BackFace(BackKind back)
        {
            switch (back)
            {
                case BackKind.Acp: return FaceKind.Acp;
                case BackKind.ClearAcrylic: return FaceKind.AcrylicClear;
                case BackKind.Metal: return FaceKind.Metal;
                default: return FaceKind.Fomax;
            }
        }

        private double BackThickness(BackKind back)
        {
            switch (back)
            {
                case BackKind.Acp: return c.AcpThickness;
                case BackKind.ClearAcrylic: return 3;
                case BackKind.Metal: return 1;
                default: return c.FomaxThickness;
            }
        }

        private string FaceName(FaceKind face, double thickness)
        {
            switch (face)
            {
                case FaceKind.AcrylicOpal: return AcrylicName("sutsimon (opal)", StandardAcrylic(thickness));
                case FaceKind.AcrylicColor: return AcrylicName("rangli", StandardAcrylic(thickness));
                case FaceKind.AcrylicClear: return AcrylicName("shaffof", StandardAcrylic(thickness));
                case FaceKind.Acp: return AcpName();
                case FaceKind.Metal: return MetalName(thickness);
                default: return FomaxName(thickness);
            }
        }

        private static double StandardAcrylic(double thickness)
        {
            foreach (double t in AcrylicThicknesses)
            {
                if (t >= thickness - 0.01)
                    return t;
            }
            return AcrylicThicknesses[AcrylicThicknesses.Length - 1];
        }

        private static string AcrylicKey(string kind, double t) => "akril-" + kind + "-" + Key(StandardAcrylic(t));
        private static string AcrylicName(string kind, double t) => "Akril " + kind + " " + Fmt(StandardAcrylic(t)) + " mm";
        private static string FomaxKey(double t) => "fomaks-" + Key(t);
        private static string FomaxName(double t) => "Fomaks (PVX) " + Fmt(t) + " mm";
        private string AcpKey() => "alyukobond-" + Key(c.AcpThickness);
        private string AcpName() => "Alyukobond " + Fmt(c.AcpThickness) + " mm";
        private static string MetalKey(double t) => "metall-list-" + Key(t);
        private static string MetalName(double t) => "Metall list " + Fmt(t) + " mm";

        private static double PolygonLength(Point[] points)
        {
            double length = 0;
            for (int i = 0; i < points.Length; i++)
            {
                Point a = points[i];
                Point b = points[(i + 1) % points.Length];
                length += Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
            }
            return length;
        }

        /// <summary>Teshiklar egallagan ulush (0..1) — bo'yaladigan yuza shuncha kamayadi.</summary>
        private static double OpenFraction(List<Hole> holes, Rect area)
        {
            double open = 0;
            foreach (Hole hole in holes)
            {
                Point[] p = hole.Points;
                double sum = 0;
                for (int i = 0; i < p.Length; i++)
                {
                    Point a = p[i];
                    Point b = p[(i + 1) % p.Length];
                    sum += a.X * b.Y - b.X * a.Y;
                }
                open += Math.Abs(sum) / 2;
            }
            return Num.Clamp(open / Math.Max(area.Width * area.Height, 1), 0, 0.95);
        }

        // ================================================================ qatorlar

        private MaterialLine Line(string key, string name, string unit, MaterialGroup group)
        {
            if (!lines.TryGetValue(key, out MaterialLine line))
            {
                line = new MaterialLine(key, name, unit, group);
                lines[key] = line;
                order.Add(line);
            }
            return line;
        }

        /// <summary>Miqdor qo'shadi va izohga "qism miqdor birlik" yozadi.</summary>
        private void Add(string key, string name, string unit, MaterialGroup group, double quantity, string part)
        {
            if (quantity <= 0)
                return;
            MaterialLine line = Line(key, name, unit, group);
            line.Quantity += quantity;
            line.AddPart(part + " " + Fmt(unit == "dona" ? Math.Ceiling(quantity - 1e-9) : quantity) + " " + unit);
        }

        /// <summary>Miqdor qo'shadi, izoh o'zgarishsiz (kley, kraska — izohda chok yoki yuza yoziladi).</summary>
        private void AddRaw(string key, string name, string unit, MaterialGroup group, double quantity, string part)
        {
            if (quantity <= 0)
                return;
            MaterialLine line = Line(key, name, unit, group);
            line.Quantity += quantity;
            line.AddPart(part);
        }

        /// <summary>Yaxlitlash, list / pogon metr / dona izohlari va guruh bo'yicha tartib.</summary>
        private void Finish()
        {
            double waste = n[Norms.SheetWaste];
            foreach (MaterialLine line in order)
            {
                double q = line.Quantity;
                switch (line.Unit)
                {
                    case "dona":
                        line.Quantity = Math.Max(1, Math.Ceiling(q - 1e-9));
                        break;
                    case "m²":
                        line.Quantity = Math.Ceiling(q * 100 - 1e-6) / 100;
                        break;
                    default:
                        line.Quantity = Math.Ceiling(q * 10 - 1e-6) / 10;
                        break;
                }

                if (line.Group == MaterialGroup.Sheets && line.Unit == "m²" && line.SheetHeight <= 0 && line.SheetWidth > 0)
                {
                    // Plyonka: rulon eni bo'yicha pogon metr
                    double meters = line.Quantity / (line.SheetWidth / 1000);
                    line.Extra = "≈ " + Fmt(Math.Ceiling(meters * 10) / 10) + " pog. m (rulon " + Fmt(line.SheetWidth) + " mm), +" +
                                 Fmt(n[Norms.FilmWaste]) + "% chiqindi bilan";
                }
                else if (line.Group == MaterialGroup.Sheets && line.Unit == "m²")
                {
                    string size = line.SheetHeight > 0 ? " (" + Fmt(line.SheetWidth) + "×" + Fmt(line.SheetHeight) + ")" : string.Empty;
                    double sheetArea = line.SheetWidth * line.SheetHeight / 1e6;
                    if (sheetArea > 0 && c.SheetUnit == SheetUnit.WholeSheets)
                    {
                        line.Extra = Fmt(line.Quantity) + " m², +" + Fmt(waste) + "% chiqindi bilan" + size;
                        line.Quantity = Math.Max(1, Math.Ceiling(line.Quantity / sheetArea - 1e-6));
                        line.Unit = "list";
                    }
                    else if (sheetArea > 0)
                    {
                        line.Extra = "≈ " + Fmt(Math.Ceiling(line.Quantity / sheetArea * 10) / 10) + " list" + size + ", +" + Fmt(waste) + "% chiqindi bilan";
                    }
                    else
                    {
                        line.Extra = "+" + Fmt(waste) + "% chiqindi bilan";
                    }
                }
                else if (line.PieceLength > 0)
                {
                    line.Extra = "≈ " + Fmt(Math.Ceiling(line.Quantity * 1000 / line.PieceLength)) + " dona × " + Fmt(line.PieceLength / 1000) + " m, +5% zaxira";
                }
            }

            result.Lines.AddRange(order.OrderBy(line => (int)line.Group).ThenBy(line => order.IndexOf(line)));
        }

        private static string Key(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string Fmt(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
