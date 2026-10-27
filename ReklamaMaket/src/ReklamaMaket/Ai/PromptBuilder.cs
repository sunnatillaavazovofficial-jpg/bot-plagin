using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Ai
{
    /// <summary>
    /// ChatGPT uchun so'rov matni: foydalanuvchi matni + plagin dizayni haqida tushuntirish
    /// (rasm qanday o'qiladi, harf va fon tuzilishi, ranglar, gul, yoritilish) + kunduzi/kechasi.
    /// </summary>
    public static class PromptBuilder
    {
        /// <summary>
        /// Asosiy so'rov (inglizcha — model inglizchani aniqroq tushunadi). Ma'nosi:
        /// "Rasmdagi logo va rasm proporsiyasini buzmagan holda logoni real, 3D qilib ko'rsat."
        /// Panelda o'zgartirish mumkin.
        /// </summary>
        public const string DefaultPrompt =
            "Keep the logo and the whole picture in their original proportions — do not stretch, redraw or change the logo, " +
            "its letters, shapes or colours. Show the logo from this picture as a real, physically manufactured 3D sign " +
            "with realistic materials, depth, lighting and shadows.";

        public static string Build(SignSettings s, RuleResult rules, string userPrompt, bool night, bool hasNightReference, bool hasLocationPhoto)
        {
            var text = new StringBuilder();
            text.AppendLine((string.IsNullOrWhiteSpace(userPrompt) ? DefaultPrompt : userPrompt).Trim());
            text.AppendLine();

            AppendImageGuide(text, s, hasNightReference, hasLocationPhoto);
            text.AppendLine();
            AppendDesign(text, s, rules);
            text.AppendLine();

            text.AppendLine("Requirements:");
            text.AppendLine("- Build exactly this construction with these materials and colours; keep every letter, shape, colour and the spacing of the logo unchanged.");
            text.AppendLine("- Keep the background type, its pattern and frame as designed; the logo must stay in the same place on the background.");
            text.AppendLine("- Do not add any extra text, logos, people, plants or decorations to the sign.");
            text.AppendLine();

            if (!night)
                text.Append("Daytime, natural daylight, the sign lights are switched off.");
            else if (rules.LettersLit || rules.BackgroundLit)
                text.Append("Night time, dark sky. The sign is switched on: " + Lighting(s, rules) + " Realistic glow without exaggerated bloom.");
            else
                text.Append("Night time with street lighting. The sign has no lights of its own and is NOT illuminated.");

            return text.ToString();
        }

        /// <summary>Yuborilgan rasmlarni qanday tushunish kerakligi.</summary>
        private static void AppendImageGuide(StringBuilder text, SignSettings s, bool hasNightReference, bool hasLocationPhoto)
        {
            int index = 1;
            text.AppendLine("How to read the attached images:");
            text.AppendLine("- Image 1 is the sign design made with the \"Reklama Maket\" CorelDRAW plugin. It is a flat, straight-on technical " +
                            "front view (not a photo) in the real colours and real proportions of the sign, drawn at scale 1:" + s.Scale + ". " +
                            "The soft dark shadow under the letters only indicates that they are three-dimensional.");
            if (hasNightReference)
            {
                index++;
                text.AppendLine("- Image " + index + " is the plugin's night preview of the same sign: bright and glowing areas show exactly which parts " +
                                "light up and how strongly; reproduce the same lighting.");
            }
            if (hasLocationPhoto)
            {
                index++;
                text.AppendLine("- Image " + index + " is a photo of the real location. Place the sign on the facade in this photo at a realistic position " +
                                "and scale, match the perspective and light, and keep the rest of the photo unchanged.");
            }
            else
            {
                text.AppendLine("- Show the sign mounted on a realistic building facade, viewed slightly from the side so the depth of the letters is visible.");
            }
        }

        /// <summary>Dizayn tavsifi: harflar, fon, o'lchamlar, qoidalar.</summary>
        private static void AppendDesign(StringBuilder text, SignSettings s, RuleResult rules)
        {
            SignLayout layout = LogoLayout.Compute(s);
            LetterStyle letter = s.Letter;
            BackgroundStyle background = s.Background;

            text.AppendLine("Sign design (as set in the plugin):");
            text.AppendLine("- Logo letters: " + letter.EnglishName + " — " + letter.PromptDetails + ".");

            string colours = s.KeepLogoColors
                ? "faces keep the original logo colours"
                : "face " + ColorMath.Describe(s.FaceColor);
            colours += ", returns (sides) " + ColorMath.Describe(s.ReturnColor);
            if (rules.LettersLit)
                colours += ", LED light " + ColorMath.Describe(s.LightColor);
            text.AppendLine("- Letter colours: " + colours + ".");

            if (rules.CanBe3D)
            {
                text.AppendLine("- Letter depth: " + Num.Format(s.Depth) + " mm" +
                                (letter.OnStandoffs ? ", mounted on about 30 mm spacers so the letters float off the background" : string.Empty) + ".");
            }
            text.AppendLine("- Logo size: " + Num.Format(s.LogoWidth) + " × " + Num.Format(s.LogoHeight) + " mm, " + Placement(s.Align) + " on the background.");

            string backgroundLine = "- Background: " + background.EnglishName + " — " + background.PromptDetails;
            if (background.HasPattern)
                backgroundLine += "; pattern: " + s.Pattern.EnglishName + " (about " + Num.Format(s.Pattern.Pitch) + " mm pitch)";
            backgroundLine += "; colour " + ColorMath.Describe(s.BackgroundColor);
            if (background.FrameWidth > 0)
                backgroundLine += "; frame " + Num.Format(background.FrameWidth) + " mm, " + ColorMath.Describe(background.FrameColor);
            if (background.Kind != BackgroundKind.Wall)
            {
                backgroundLine += "; size " + Num.Format(System.Math.Round(layout.BackgroundWidth)) + " × " +
                                  Num.Format(System.Math.Round(layout.BackgroundHeight)) + " mm";
            }
            text.AppendLine(backgroundLine + ".");

            if (background.SupportsLighting)
                text.AppendLine("- Background lighting: " + (rules.BackgroundLit ? BackgroundLights.Prompt(s.BackgroundLight) : BackgroundLights.Prompt(BackgroundLight.None)) + ".");
            else if (background.Kind == BackgroundKind.Lightbox)
                text.AppendLine("- Background lighting: " + (rules.BackgroundLit ? "the lightbox face glows evenly" : "the lightbox is not illuminated") + ".");

            if (!rules.CanBe3D)
                text.AppendLine("- Important: the logo is smaller than 20 cm, so it is made as FLAT cut-out letters with no depth, and it is NOT illuminated.");
            else if (letter.Illuminated && !rules.CanLight)
                text.AppendLine("- Important: the letters are thinner than 4 cm, so they are NOT illuminated (no LEDs inside).");
        }

        private static string Placement(LogoAlign align)
        {
            switch (align)
            {
                case LogoAlign.Left: return "aligned to the left";
                case LogoAlign.Right: return "aligned to the right";
                case LogoAlign.Top: return "aligned to the top";
                case LogoAlign.Bottom: return "aligned to the bottom";
                default: return "centred";
            }
        }

        private static string Lighting(SignSettings s, RuleResult rules)
        {
            var parts = new List<string>();
            if (rules.LettersLit)
            {
                LetterStyle letter = s.Letter;
                if (letter.Has(LightZones.Face)) parts.Add("the letter faces glow");
                if (letter.Has(LightZones.Halo)) parts.Add("a soft halo of light glows on the wall behind the letters");
                if (letter.Has(LightZones.SideFull)) parts.Add("the letter sides glow");
                if (letter.Has(LightZones.SidePartial)) parts.Add("a thin line of light glows along the letter sides");
                if (letter.Has(LightZones.Rim)) parts.Add("the letter outline glows like neon");
                if (letter.Has(LightZones.Dots)) parts.Add("the LED points or bulbs on the face shine");
                if (letter.Has(LightZones.Pattern)) parts.Add("warm light shines through the cut pattern");
            }
            if (rules.BackgroundLit)
            {
                parts.Add(s.Background.Kind == BackgroundKind.Lightbox
                    ? "the lightbox background glows evenly"
                    : BackgroundLights.Prompt(s.BackgroundLight));
            }

            return string.Join("; ", parts) + ". Light colour: " + ColorName(s.LightColor) + ".";
        }

        private static string ColorName(Color c)
        {
            if (c.R > 235 && c.G > 235 && c.B > 235)
                return "cool white (6500 K)";
            if (c.R > 230 && c.G > 170 && c.B < 170)
                return "warm white (3000 K)";
            return ColorMath.Describe(c);
        }
    }
}
