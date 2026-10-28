using System.Collections.Generic;
using System.Linq;

namespace ReklamaRasxod.Estimate
{
    /// <summary>Panelda normalar shu guruhlarga bo'linadi.</summary>
    public enum NormGroup
    {
        Sheets,
        Electric,
        Consumables
    }

    /// <summary>Bitta sarf normasi: nomi, birligi, standart qiymati va chegaralari.</summary>
    public sealed class NormInfo
    {
        public NormInfo(string key, NormGroup group, string title, string unit, double value, double min, double max, string hint)
        {
            Key = key;
            Group = group;
            Title = title;
            Unit = unit;
            Default = value;
            Min = min;
            Max = max;
            Hint = hint;
        }

        public string Key { get; }
        public NormGroup Group { get; }
        public string Title { get; }
        public string Unit { get; }
        public double Default { get; }
        public double Min { get; }
        public double Max { get; }
        public string Hint { get; }
    }

    /// <summary>
    /// Sarf normalari (chiqindi foizi, list o'lchamlari, LED quvvati, samorez qadami, kraska sarfi...).
    /// Foydalanuvchi o'zgartirgan qiymatlar <see cref="Values"/> da saqlanadi, qolganlari standart.
    /// </summary>
    public sealed class Norms
    {
        public const string SheetWaste = "waste.sheet";
        public const string FilmWaste = "waste.film";
        public const string AcrylicSheetW = "sheet.acrylic.w";
        public const string AcrylicSheetH = "sheet.acrylic.h";
        public const string FomaxSheetW = "sheet.fomax.w";
        public const string FomaxSheetH = "sheet.fomax.h";
        public const string AcpSheetW = "sheet.acp.w";
        public const string AcpSheetH = "sheet.acp.h";
        public const string MetalSheetW = "sheet.metal.w";
        public const string MetalSheetH = "sheet.metal.h";
        public const string FilmRoll = "roll.film";
        public const string TubeLength = "tube.length";

        public const string LedPower = "led.power";
        public const string LedStep = "led.step";
        public const string StripPower = "strip.power";
        public const string DotPower = "dot.power";
        public const string BulbPower = "bulb.power";
        public const string PsuPower = "psu.power";
        public const string PsuReserve = "psu.reserve";
        public const string WireToPsu = "wire.psu";
        public const string WirePerLetter = "wire.letter";
        public const string MainsCable = "cable.mains";

        public const string FixArea = "fix.area";
        public const string AssemblyScrewStep = "screw.assembly";
        public const string PanelScrewStep = "screw.panel";
        public const string AnchorStep = "anchor.step";
        public const string FrameStep = "frame.step";
        public const string GlueRate = "glue.rate";
        public const string SealantRate = "sealant.rate";
        public const string PaintRate = "paint.rate";
        public const string PaintCoats = "paint.coats";
        public const string PrimerRate = "primer.rate";

        public static readonly NormInfo[] All =
        {
            new NormInfo(SheetWaste, NormGroup.Sheets, "Listlar chiqindisi", "%", 25, 0, 300,
                "Akril, fomaks, alyukobond va metall listdan kesishda qoladigan chiqindi. Harf qancha mayda bo'lsa, shuncha ko'p."),
            new NormInfo(FilmWaste, NormGroup.Sheets, "Orakal chiqindisi", "%", 20, 0, 300, "Plyonkani kesish va yopishtirishdagi chiqindi."),
            new NormInfo(AcrylicSheetW, NormGroup.Sheets, "Akril listi — eni", "mm", 2050, 100, 10000, "Bitta akril listining o'lchami."),
            new NormInfo(AcrylicSheetH, NormGroup.Sheets, "Akril listi — bo'yi", "mm", 3050, 100, 10000, "Bitta akril listining o'lchami."),
            new NormInfo(FomaxSheetW, NormGroup.Sheets, "Fomaks listi — eni", "mm", 1220, 100, 10000, "Bitta fomaks (PVX) listining o'lchami."),
            new NormInfo(FomaxSheetH, NormGroup.Sheets, "Fomaks listi — bo'yi", "mm", 2440, 100, 10000, "Bitta fomaks (PVX) listining o'lchami."),
            new NormInfo(AcpSheetW, NormGroup.Sheets, "Alyukobond listi — eni", "mm", 1220, 100, 10000, "Bitta alyukobond listining o'lchami."),
            new NormInfo(AcpSheetH, NormGroup.Sheets, "Alyukobond listi — bo'yi", "mm", 2440, 100, 10000, "Bitta alyukobond listining o'lchami."),
            new NormInfo(MetalSheetW, NormGroup.Sheets, "Metall list — eni", "mm", 1250, 100, 10000, "Bitta metall (po'lat) listning o'lchami."),
            new NormInfo(MetalSheetH, NormGroup.Sheets, "Metall list — bo'yi", "mm", 2500, 100, 10000, "Bitta metall (po'lat) listning o'lchami."),
            new NormInfo(FilmRoll, NormGroup.Sheets, "Orakal rulon eni", "mm", 1260, 100, 5000, "Pogon metrni hisoblash uchun."),
            new NormInfo(TubeLength, NormGroup.Sheets, "Profil quvur uzunligi (1 dona)", "mm", 6000, 500, 20000, "Profil necha donaligini hisoblash uchun."),

            new NormInfo(LedPower, NormGroup.Electric, "LED modul quvvati", "W", 0.72, 0.05, 20, "Bitta 12V LED modulning quvvati."),
            new NormInfo(LedStep, NormGroup.Electric, "Modul qadami = qalinlik ×", "", 1.5, 0.3, 6,
                "Modullar orasidagi masofa harf (quti) qalinligiga bog'liq: qadam = qalinlik × shu son (50–250 mm). Kichik son — ko'proq modul."),
            new NormInfo(StripPower, NormGroup.Electric, "LED / neon lenta quvvati", "W/m", 9.6, 0.5, 50, "1 metr lentaning quvvati."),
            new NormInfo(DotPower, NormGroup.Electric, "Ochiq LED / piksel quvvati", "W", 0.2, 0.01, 10, "Bitta ochiq LED nuqta yoki pikselning quvvati."),
            new NormInfo(BulbPower, NormGroup.Electric, "Lampochka quvvati (220V)", "W", 1, 0.1, 100, "Bitta lampochkaning quvvati."),
            new NormInfo(PsuPower, NormGroup.Electric, "Blok pitaniya quvvati", "W", 200, 10, 2000, "Bitta 12V blok pitaniyaning quvvati."),
            new NormInfo(PsuReserve, NormGroup.Electric, "Blok zaxirasi", "%", 20, 0, 200, "Blok to'liq yuklanmasligi uchun quvvat zaxirasi."),
            new NormInfo(WireToPsu, NormGroup.Electric, "Logodan blokgacha sim", "m", 3, 0, 500, "Blok pitaniya logodan qancha uzoqda."),
            new NormInfo(WirePerLetter, NormGroup.Electric, "Har bir harfga ulash simi", "m", 0.6, 0, 20, "Harflarni bir-biriga ulash uchun."),
            new NormInfo(MainsCable, NormGroup.Electric, "220V kabel (tarmoqqacha)", "m", 5, 0, 1000, "Blokdan elektr tarmog'igacha."),

            new NormInfo(FixArea, NormGroup.Consumables, "Mahkamlash: 1 nuqta har", "m²", 0.05, 0.005, 5,
                "Harfni fon yoki devorga mahkamlash nuqtalari: yuzaning shuncha qismiga bitta (har harfga kamida 2 ta)."),
            new NormInfo(AssemblyScrewStep, NormGroup.Consumables, "Harf yig'ish samorezi qadami", "mm", 150, 0, 2000,
                "Bort va orqa qismni biriktiruvchi samorezlar orasidagi masofa. 0 — samorez ishlatilmaydi (faqat kley)."),
            new NormInfo(PanelScrewStep, NormGroup.Consumables, "Panelni karkasga samorez qadami", "mm", 300, 50, 2000, "Alyukobond / list karkasga shu oraliqda."),
            new NormInfo(AnchorStep, NormGroup.Consumables, "Devorga dyubel (anker) qadami", "mm", 600, 100, 5000, "Karkas devorga shu oraliqda mahkamlanadi."),
            new NormInfo(FrameStep, NormGroup.Consumables, "Karkas ustunlari oralig'i", "mm", 600, 200, 5000, "Karkas ichidagi oraliq ustunlar qadami."),
            new NormInfo(GlueRate, NormGroup.Consumables, "1 flakon kley yetadi", "m chok", 20, 1, 500, "Bitta flakon (200 g) kley bilan qancha metr chok yopishtiriladi."),
            new NormInfo(SealantRate, NormGroup.Consumables, "1 tuba germetik yetadi", "m chok", 12, 1, 500, "Bitta tuba (280 ml) silikon germetik bilan qancha metr chok yopiladi."),
            new NormInfo(PaintRate, NormGroup.Consumables, "Kraska sarfi (1 qavat)", "kg/m²", 0.12, 0.01, 2, "Emal kraskaning bir qavat uchun sarfi."),
            new NormInfo(PaintCoats, NormGroup.Consumables, "Kraska qavatlari", "", 2, 1, 10, "Necha qavat bo'yaladi."),
            new NormInfo(PrimerRate, NormGroup.Consumables, "Gruntovka sarfi", "kg/m²", 0.1, 0, 2, "Metallni bo'yashdan oldin. 0 — gruntlanmaydi."),
        };

        private static readonly Dictionary<string, NormInfo> ByKey = All.ToDictionary(n => n.Key);

        /// <summary>Foydalanuvchi o'zgartirgan qiymatlar (kalit — <see cref="NormInfo.Key"/>).</summary>
        public Dictionary<string, double> Values { get; } = new Dictionary<string, double>();

        public static NormInfo Info(string key) => ByKey[key];

        public double this[string key]
        {
            get
            {
                NormInfo info = ByKey[key];
                if (!Values.TryGetValue(key, out double value))
                    return info.Default;
                if (value < info.Min) return info.Min;
                if (value > info.Max) return info.Max;
                return value;
            }
        }

        public Norms Clone()
        {
            var copy = new Norms();
            foreach (KeyValuePair<string, double> pair in Values)
                copy.Values[pair.Key] = pair.Value;
            return copy;
        }
    }
}
