using System.Collections.Generic;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Core
{
    /// <summary>Qoidalar tekshiruvi natijasi.</summary>
    public sealed class RuleResult
    {
        /// <summary>Hajmli (3D) qilish mumkin.</summary>
        public bool CanBe3D { get; set; }

        /// <summary>Yoritish mumkin (qoidalar bo'yicha).</summary>
        public bool CanLight { get; set; }

        /// <summary>Tanlangan harf yonadi: turi yonadigan va qoidalar ruxsat beradi.</summary>
        public bool LettersLit { get; set; }

        /// <summary>Laytboks fon yonadi.</summary>
        public bool BackgroundLit { get; set; }

        public List<string> Warnings { get; } = new List<string>();

        public bool HasWarnings => Warnings.Count > 0;
    }

    /// <summary>
    /// Ishlab chiqarish qoidalari:
    /// 20 sm dan past logoni hajmli (3D) va yoritilgan qilib bo'lmaydi;
    /// qalinligi 4 sm dan kam logoni yoritib bo'lmaydi.
    /// </summary>
    public static class SignRules
    {
        public const double Min3DHeightMm = 200;
        public const double MinLitDepthMm = 40;

        public static RuleResult Check(SignSettings s)
        {
            var result = new RuleResult
            {
                CanBe3D = s.LogoHeight >= Min3DHeightMm,
            };
            result.CanLight = result.CanBe3D && s.Depth >= MinLitDepthMm;
            result.LettersLit = s.Letter.Illuminated && result.CanLight;

            if (!result.CanBe3D)
            {
                result.Warnings.Add(string.Format(
                    "Logo balandligi {0} sm — 20 sm dan past. Bunday logoni hajmli (3D) va yoritilgan qilib bo'lmaydi: faqat yassi harf bo'ladi.",
                    Num.Format(s.LogoHeight / 10)));
            }
            else if (s.Depth < MinLitDepthMm && s.Letter.Illuminated)
            {
                result.Warnings.Add(string.Format(
                    "Qalinligi {0} sm — 4 sm dan kam. Bunday harfni yoritib bo'lmaydi (ichiga LED sig'maydi).",
                    Num.Format(s.Depth / 10)));
            }

            if (s.Background.Kind == BackgroundKind.Lightbox)
            {
                result.BackgroundLit = s.BackgroundDepth >= MinLitDepthMm;
                if (!result.BackgroundLit)
                {
                    result.Warnings.Add(string.Format(
                        "Laytboks qalinligi {0} sm — 4 sm dan kam, uni yoritib bo'lmaydi.",
                        Num.Format(s.BackgroundDepth / 10)));
                }
            }

            if (!s.AutoBackgroundSize && s.Background.Kind != BackgroundKind.Wall &&
                (s.BackgroundWidth < s.LogoWidth + 2 * s.Margin || s.BackgroundHeight < s.LogoHeight + 2 * s.Margin))
            {
                result.Warnings.Add("Fon logo va chetdan joy uchun kichik — logo fondan chiqib ketadi.");
            }

            return result;
        }
    }
}
