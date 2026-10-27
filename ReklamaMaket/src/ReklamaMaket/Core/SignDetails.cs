using System;
using System.Windows.Media;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Core
{
    /// <summary>Rangi va o'lchamini alohida o'zgartirsa bo'ladigan detallar.</summary>
    public enum Detail
    {
        Trim,
        Standoff,
        Protrusion,
        Dots,
        FacePattern,
        Glow,
        BackgroundLight,
        Frame,
        Pattern,
        SlatWidth,
        SlatGap,
        Border,
        BorderInset,
        Wall,
        Bolts
    }

    /// <summary>Panelda detallar shu guruhlarga bo'linadi.</summary>
    public enum DetailGroup
    {
        Logo,
        Light,
        Background
    }

    /// <summary>Qaysi tanlov o'zgarganda foydalanuvchi qiymati bekor qilinadi (yangi dizaynning o'z qiymati olinadi).</summary>
    public enum DetailReset
    {
        Never,
        Letter,
        Background,
        Pattern
    }

    public sealed class DetailInfo
    {
        public DetailInfo(Detail key, DetailGroup group, bool hasColor, bool hasSize, double minSize, DetailReset reset, string hint)
        {
            Key = key;
            Group = group;
            HasColor = hasColor;
            HasSize = hasSize;
            MinSize = minSize;
            Reset = reset;
            Hint = hint;
        }

        public Detail Key { get; }
        public DetailGroup Group { get; }
        public bool HasColor { get; }
        public bool HasSize { get; }

        /// <summary>Eng kichik o'lcham, mm.</summary>
        public double MinSize { get; }

        public DetailReset Reset { get; }

        /// <summary>Panelda izoh (tooltip).</summary>
        public string Hint { get; }
    }

    /// <summary>
    /// Detallarning rangi va o'lchami: foydalanuvchi o'zgartirgan bo'lsa — o'sha qiymat
    /// (<see cref="SignSettings.DetailColors"/>, <see cref="SignSettings.DetailSizes"/>), bo'lmasa — dizayn turidagi
    /// yoki logo/fon o'lchamidan hisoblangan qiymat. Corel chizmasi, panel ko'rinishi va ChatGPT so'rovi shu bitta hisobdan foydalanadi.
    /// </summary>
    public static class SignDetails
    {
        public static readonly DetailInfo[] All =
        {
            new DetailInfo(Detail.Trim, DetailGroup.Logo, false, true, 0, DetailReset.Letter,
                "Yuz atrofidagi bort qirrasi (kant). Rangi — «Bort» rangi. 0 — kantsiz."),
            new DetailInfo(Detail.Standoff, DetailGroup.Logo, false, true, 0, DetailReset.Never,
                "Harf va fon (devor) orasidagi masofa — distansiya boltlari."),
            new DetailInfo(Detail.Protrusion, DetailGroup.Logo, false, true, 0, DetailReset.Letter,
                "Akril harf yuzdan qancha chiqib turadi (akril qalinligidan oshmaydi)."),
            new DetailInfo(Detail.Dots, DetailGroup.Logo, true, true, 1, DetailReset.Letter,
                "Kunduzgi rangi va diametri. Kechasi nur rangida yonadi."),
            new DetailInfo(Detail.FacePattern, DetailGroup.Logo, true, true, 2, DetailReset.Letter,
                "Yuzdagi lazer naqsh elementi: kunduzgi rangi va kattaligi."),
            new DetailInfo(Detail.Glow, DetailGroup.Light, false, true, 0, DetailReset.Never,
                "Kechasi nur logo atrofiga qancha tarqaladi (kontrajur, yon nur)."),
            new DetailInfo(Detail.BackgroundLight, DetailGroup.Light, true, false, 0, DetailReset.Never,
                "Fon orqasidagi LED nuri rangi (teshiklar va reykalar orasidan chiqadi)."),
            new DetailInfo(Detail.Frame, DetailGroup.Background, true, true, 0, DetailReset.Background,
                "Fon ramkasi (profil) rangi va eni. 0 — ramkasiz."),
            new DetailInfo(Detail.Pattern, DetailGroup.Background, false, true, 3, DetailReset.Pattern,
                "Gul teshiklari qadami (markazdan markazga) — gul kattaligi."),
            new DetailInfo(Detail.SlatWidth, DetailGroup.Background, false, true, 5, DetailReset.Background,
                "Bitta reyka (lamel) eni."),
            new DetailInfo(Detail.SlatGap, DetailGroup.Background, false, true, 0, DetailReset.Background,
                "Reykalar orasidagi bo'shliq."),
            new DetailInfo(Detail.Border, DetailGroup.Background, true, true, 0.5, DetailReset.Never,
                "Fon chetidagi o'yilgan chiziq rangi va eni."),
            new DetailInfo(Detail.BorderInset, DetailGroup.Background, false, true, 0, DetailReset.Never,
                "O'yilgan chiziq fon chetidan qancha ichkarida."),
            new DetailInfo(Detail.Wall, DetailGroup.Background, true, false, 0, DetailReset.Never,
                "Fon orqasidagi devor — teshik va reyka oralig'idan ko'rinadi."),
            new DetailInfo(Detail.Bolts, DetailGroup.Background, true, true, 3, DetailReset.Background,
                "Akril plitani ushlab turgan boltlar rangi va diametri."),
        };

        public static DetailInfo Info(Detail detail) => All[(int)detail];

        /// <summary>Detal tanlangan harf va fon uchun bormi (panelda faqat shular ko'rsatiladi).</summary>
        public static bool Applies(SignSettings s, Detail detail)
        {
            LetterStyle letter = s.Letter;
            BackgroundStyle background = s.Background;
            switch (detail)
            {
                case Detail.Trim: return !letter.IsCut || letter.Cut == CutMode.PushThrough;
                case Detail.Standoff: return letter.OnStandoffs && !letter.IsCut;
                case Detail.Protrusion: return letter.Cut == CutMode.PushThrough;
                case Detail.Dots: return letter.Has(LightZones.Dots);
                case Detail.FacePattern: return letter.Has(LightZones.Pattern);
                case Detail.Glow: return letter.Illuminated;
                case Detail.BackgroundLight: return background.SupportsLighting;
                case Detail.Frame: return background.Kind == BackgroundKind.Lightbox || background.HasPattern;
                case Detail.Pattern: return background.HasPattern;
                case Detail.SlatWidth:
                case Detail.SlatGap: return background.HasSlats;
                case Detail.Border:
                case Detail.BorderInset: return s.EngravedBorder && background.CanHaveBorder;
                // Orqasi yoritilsa teshiklardan devor emas, oq diffuzor ko'rinadi
                case Detail.Wall: return background.Kind == BackgroundKind.HorizontalSlats || (background.HasPattern && s.BackgroundLight == BackgroundLight.None);
                case Detail.Bolts: return background.Kind == BackgroundKind.AcrylicPlate;
                default: return false;
            }
        }

        /// <summary>Panelda ko'rinadigan nomi (dizayn turiga mos).</summary>
        public static string Title(SignSettings s, Detail detail)
        {
            switch (detail)
            {
                case Detail.Trim: return s.Letter.Cut == CutMode.PushThrough ? "Akril qirrasi eni" : "Kant (bort qirrasi) eni";
                case Detail.Standoff: return "Distansiya (fondan masofa)";
                case Detail.Protrusion: return "Harf chiqib turishi";
                case Detail.Dots:
                    return s.Letter.Face == FaceMaterial.Metal ? "Lampochkalar (diametri)"
                        : s.Letter.Face == FaceMaterial.Perforated ? "Perforatsiya teshiklari (diametri)"
                        : "LED nuqtalar (diametri)";
                case Detail.FacePattern: return "Yuzdagi naqsh (kattaligi)";
                case Detail.Glow: return "Nur tarqalishi";
                case Detail.BackgroundLight: return "Fon nuri rangi";
                case Detail.Frame:
                    return s.Background.Kind == BackgroundKind.Lightbox ? "Laytboks profili (ramka)"
                        : s.Background.Kind == BackgroundKind.WoodFrameMesh ? "Yog'och ramka" : "Ramka";
                case Detail.Pattern: return "Gul o'lchami (qadami)";
                case Detail.SlatWidth: return "Reyka eni";
                case Detail.SlatGap: return "Reykalar oralig'i";
                case Detail.Border: return "O'yilgan ramka chizig'i";
                case Detail.BorderInset: return "Chiziq fon chetidan";
                case Detail.Wall: return "Devor rangi (orqada)";
                case Detail.Bolts: return "Distansiya boltlari";
                default: return detail.ToString();
            }
        }

        // ================================================================ qiymatlar

        public static Color ColorOf(SignSettings s, Detail detail) =>
            s.DetailColors.TryGetValue(detail, out Color color) ? color : DefaultColor(s, detail);

        public static double SizeOf(SignSettings s, Detail detail)
        {
            double size = s.DetailSizes.TryGetValue(detail, out double value)
                ? Math.Max(value, Info(detail).MinSize)
                : DefaultSize(s, detail);
            // Akril harf o'z qalinligidan ortiq chiqolmaydi
            return detail == Detail.Protrusion ? Math.Min(size, s.Depth) : size;
        }

        public static Color DefaultColor(SignSettings s, Detail detail)
        {
            switch (detail)
            {
                case Detail.Dots:
                    return s.Letter.Face == FaceMaterial.Perforated ? ColorMath.Shade(s.FaceColor, 2.4)
                        : s.Letter.Face == FaceMaterial.Metal ? ColorMath.Rgb(0xF3EBD8)
                        : ColorMath.Rgb(0xD9D9D9);
                case Detail.FacePattern: return ColorMath.Rgb(0xE8C766);
                case Detail.BackgroundLight: return s.LightColor;
                case Detail.Frame: return s.Background.Kind == BackgroundKind.Lightbox ? ColorMath.Steel : s.Background.FrameColor;
                case Detail.Border: return ColorMath.Silver;
                case Detail.Wall: return ColorMath.WallBehind;
                case Detail.Bolts: return ColorMath.Steel;
                default: return Colors.Gray;
            }
        }

        public static double DefaultSize(SignSettings s, Detail detail)
        {
            LetterStyle letter = s.Letter;
            switch (detail)
            {
                case Detail.Trim: return letter.Cut == CutMode.PushThrough ? Math.Max(letter.TrimMm, 3) : letter.TrimMm;
                case Detail.Standoff: return 30;
                case Detail.Protrusion: return letter.Protrusion(s.Depth);
                case Detail.Dots:
                    if (letter.Face == FaceMaterial.Perforated)
                        return Num.Clamp(s.LogoHeight * 0.03, 8, 30) * 0.5;
                    if (letter.Face == FaceMaterial.Metal)
                        return Num.Clamp(s.LogoHeight * 0.12, 40, 200) * 0.55;
                    return Num.Clamp(s.LogoHeight * 0.07, 20, 100) * 0.35;
                case Detail.FacePattern: return Num.Clamp(s.LogoHeight * 0.08, 25, 120) * 0.5;
                case Detail.Glow: return Num.Clamp(s.LogoHeight * 0.06, 15, 150);
                case Detail.Frame:
                    return s.Background.Kind == BackgroundKind.Lightbox ? Num.Clamp(MinSide(s) * 0.03, 25, 60) : s.Background.FrameWidth;
                case Detail.Pattern: return s.Pattern.Pitch;
                case Detail.SlatWidth: return PatternGeometry.DefaultSlatWidth(SlatSpan(s), s.Background.Kind == BackgroundKind.HorizontalSlats);
                case Detail.SlatGap: return PatternGeometry.DefaultSlatGap(SizeOf(s, Detail.SlatWidth), s.Background.Kind == BackgroundKind.HorizontalSlats);
                case Detail.Border: return Num.Clamp(MinSide(s) * 0.008, 4, 12);
                case Detail.BorderInset: return Num.Clamp(MinSide(s) * 0.04, 25, 80);
                case Detail.Bolts: return Num.Clamp(MinSide(s) * 0.03, 20, 40);
                default: return 0;
            }
        }

        // ================================================================ hisoblangan qiymatlar

        /// <summary>LED nuqtalar / lampochkalar orasidagi qadam, mm.</summary>
        public static double DotSpacing(SignSettings s)
        {
            double ratio = s.Letter.Face == FaceMaterial.Perforated ? 0.5 : s.Letter.Face == FaceMaterial.Metal ? 0.55 : 0.35;
            return SizeOf(s, Detail.Dots) / ratio;
        }

        /// <summary>Yuzdagi naqsh elementlari orasidagi qadam, mm.</summary>
        public static double FacePatternSpacing(SignSettings s) => SizeOf(s, Detail.FacePattern) * 2;

        /// <summary>Tanlangan gul, foydalanuvchi o'zgartirgan qadam bilan.</summary>
        public static PatternStyle Pattern(SignSettings s)
        {
            double pitch = SizeOf(s, Detail.Pattern);
            return Math.Abs(pitch - s.Pattern.Pitch) < 0.001 ? s.Pattern : s.Pattern.WithPitch(pitch);
        }

        private static double MinSide(SignSettings s)
        {
            SignLayout layout = LogoLayout.Compute(s);
            return Math.Min(layout.BackgroundWidth, layout.BackgroundHeight);
        }

        private static double SlatSpan(SignSettings s)
        {
            SignLayout layout = LogoLayout.Compute(s);
            return s.Background.Kind == BackgroundKind.HorizontalSlats ? layout.BackgroundHeight : layout.BackgroundWidth;
        }
    }
}
