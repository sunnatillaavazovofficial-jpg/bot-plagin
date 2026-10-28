using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ReklamaRasxod.Estimate
{
    /// <summary>Rasxod jadvalidagi guruhlar (panelda shu tartibda).</summary>
    public enum MaterialGroup
    {
        Sheets,
        Profiles,
        Electric,
        Fasteners,
        Chemicals,
        Processing
    }

    /// <summary>Listli material turi — list o'lchami normalardan olinadi.</summary>
    public enum SheetKind
    {
        None,
        Acrylic,
        Fomax,
        Acp,
        Metal
    }

    /// <summary>Rasxod jadvalining bitta qatori: material, miqdor, birlik va qayerga ketgani.</summary>
    public sealed class MaterialLine
    {
        private readonly List<string> parts = new List<string>();

        public MaterialLine(string key, string name, string unit, MaterialGroup group)
        {
            Key = key;
            Name = name;
            Unit = unit;
            Group = group;
        }

        /// <summary>Narx shu kalit bo'yicha saqlanadi (birlik ham kalitga kiradi: m² va list narxi alohida).</summary>
        public string Key { get; }

        public string Name { get; }
        public string Unit { get; set; }
        public MaterialGroup Group { get; }

        /// <summary>Hisoblangan miqdor (birlikka mos yaxlitlangan).</summary>
        public double Quantity { get; set; }

        public SheetKind Sheet { get; set; }

        /// <summary>List o'lchami (mm) — "≈ N list" izohi uchun.</summary>
        public double SheetWidth { get; set; }
        public double SheetHeight { get; set; }

        /// <summary>Bir dona uzunligi (profil quvur), mm.</summary>
        public double PieceLength { get; set; }

        /// <summary>Qisqa izoh: list soni, pogon metr va hokazo.</summary>
        public string Extra { get; set; }

        /// <summary>Qayerga ketgani: "harf yuzi 0.42 m²; fon 1.2 m²".</summary>
        public IReadOnlyList<string> Parts => parts;

        public string PriceKey => Key + "|" + Unit;

        public void AddPart(string text)
        {
            if (!string.IsNullOrEmpty(text))
                parts.Add(text);
        }

        public string Note => string.Join("; ", parts);

        public override string ToString() => Name + ": " + Quantity.ToString(CultureInfo.InvariantCulture) + " " + Unit;
    }

    /// <summary>Hisob natijasi.</summary>
    public sealed class EstimateResult
    {
        public List<MaterialLine> Lines { get; } = new List<MaterialLine>();

        /// <summary>12V iste'molchilar quvvati, W.</summary>
        public double Watts12 { get; set; }

        /// <summary>220V iste'molchilar (lampochkalar) quvvati, W.</summary>
        public double Watts220 { get; set; }

        /// <summary>Hisob haqida eslatmalar (yassi harf, logo topilmadi va hokazo).</summary>
        public List<string> Notes { get; } = new List<string>();

        /// <summary>Konstruksiya qisqacha: "Yuz: akril 3 mm", "Bort: alyuminiy lenta 80 mm"...</summary>
        public List<string> Construction { get; } = new List<string>();

        /// <summary>Logoning haqiqiy o'lchamdagi yuzasi, konturi va bo'laklari.</summary>
        public ReklamaRasxod.Core.LogoMeasures Measures { get; set; }

        public MaterialLine Find(string key) => Lines.FirstOrDefault(line => line.Key == key);
    }

    public static class MaterialGroups
    {
        public static readonly MaterialGroup[] All = (MaterialGroup[])Enum.GetValues(typeof(MaterialGroup));

        public static string Title(MaterialGroup group)
        {
            switch (group)
            {
                case MaterialGroup.Sheets: return "LIST MATERIALLAR VA PLYONKA";
                case MaterialGroup.Profiles: return "TEMIR, PROFIL, BORT";
                case MaterialGroup.Electric: return "ELEKTR: DIOD, BLOK, SIM";
                case MaterialGroup.Fasteners: return "MAHKAMLASH";
                case MaterialGroup.Chemicals: return "KLEY, GERMETIK, KRASKA";
                default: return "ISHLOV BERISH (KESISH)";
            }
        }
    }
}
