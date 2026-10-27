using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Ai
{
    /// <summary>ChatGPT uchun so'rov matni: foydalanuvchi matni + dizayn ma'lumotlari + kunduzi/kechasi.</summary>
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

            int index = 1;
            text.AppendLine("Image 1 is the sign design drawn in CorelDRAW (straight front view).");
            if (hasNightReference)
            {
                index++;
                text.AppendLine("Image " + index + " is the night version of the same design: it shows which parts light up.");
            }
            if (hasLocationPhoto)
            {
                index++;
                text.AppendLine("Image " + index + " is a photo of the real location. Place the sign on the facade in this photo at a " +
                                "realistic position and scale, match the perspective and light, and keep the rest of the photo unchanged.");
            }
            else
            {
                text.AppendLine("Show the sign mounted on a realistic building facade, viewed slightly from the side so the depth of the letters is visible.");
            }

            text.AppendLine();
            text.AppendLine("Sign construction: " + s.Letter.PromptDetails + "; " + s.Background.PromptDetails + ".");
            text.Append("Real size: logo " + Num.Format(s.LogoWidth) + " × " + Num.Format(s.LogoHeight) + " mm");
            text.AppendLine(rules.CanBe3D ? ", letter depth " + Num.Format(s.Depth) + " mm." : ".");

            if (!rules.CanBe3D)
                text.AppendLine("Important: the logo is smaller than 20 cm, so it is made as FLAT cut-out letters with no depth, and it is NOT illuminated.");
            else if (s.Letter.Illuminated && !rules.CanLight)
                text.AppendLine("Important: the letters are thinner than 4 cm, so they are NOT illuminated (no LEDs inside).");

            text.AppendLine();
            if (!night)
                text.Append("Daytime, natural daylight, the sign lights are switched off.");
            else if (rules.LettersLit || rules.BackgroundLit)
                text.Append("Night time, dark sky. The sign is switched on: " + Lighting(s, rules) + " Realistic glow without exaggerated bloom.");
            else
                text.Append("Night time with street lighting. The sign has no lights of its own and is NOT illuminated.");

            return text.ToString();
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
                parts.Add("the lightbox background glows evenly");

            return string.Join(", ", parts) + ". Light colour: " + ColorName(s.LightColor) + ".";
        }

        private static string ColorName(Color c)
        {
            if (c.R > 235 && c.G > 235 && c.B > 235)
                return "cool white (6500 K)";
            if (c.R > 230 && c.G > 170 && c.B < 170)
                return "warm white (3000 K)";
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }
    }
}
