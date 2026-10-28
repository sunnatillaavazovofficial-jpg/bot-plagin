using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using ReklamaRasxod.Estimate;

namespace ReklamaRasxod.Core
{
    /// <summary>Smetaga qo'lda qo'shilgan qator (ish haqi, montaj, transport...).</summary>
    [DataContract]
    public sealed class CustomRow
    {
        [DataMember] public string Name { get; set; } = string.Empty;
        [DataMember] public string Unit { get; set; } = "dona";
        [DataMember] public double Quantity { get; set; } = 1;
        [DataMember] public double Price { get; set; }

        public double Sum => Quantity * Price;
    }

    /// <summary>
    /// Plagin sozlamalari: %APPDATA%\ReklamaRasxod\settings.json — material narxlari, sarf normalari,
    /// konstruksiya tanlovlari, qo'shimcha qatorlar va ustama. Narx kaliti: material + birlik.
    /// </summary>
    [DataContract]
    public sealed class RasxodSettings
    {
        [DataMember] public Dictionary<string, double> Prices { get; set; } = new Dictionary<string, double>();
        [DataMember] public Dictionary<string, double> Norms { get; set; } = new Dictionary<string, double>();
        [DataMember] public List<CustomRow> CustomRows { get; set; }
        [DataMember] public double Markup { get; set; }
        [DataMember] public string Currency { get; set; } = "so'm";

        [DataMember] public string Returns { get; set; }
        [DataMember] public string Back { get; set; }
        [DataMember] public double FomaxThickness { get; set; } = 5;
        [DataMember] public double AcpThickness { get; set; } = 4;
        [DataMember] public string Profile { get; set; } = "20x20";
        [DataMember] public bool PaintReturns { get; set; }
        [DataMember] public bool WholeSheets { get; set; }

        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReklamaRasxod");

        private static string FilePath => Path.Combine(Folder, "settings.json");

        public static RasxodSettings Load()
        {
            RasxodSettings settings = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    using (FileStream stream = File.OpenRead(FilePath))
                        settings = (RasxodSettings)Serializer().ReadObject(stream);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReklamaRasxod: sozlamalarni o'qib bo'lmadi: " + ex);
            }

            settings = settings ?? new RasxodSettings();
            // Eski / buzilgan faylda bo'sh maydonlar null bo'lib qolmasin
            settings.Prices = settings.Prices ?? new Dictionary<string, double>();
            settings.Norms = settings.Norms ?? new Dictionary<string, double>();
            if (string.IsNullOrWhiteSpace(settings.Currency))
                settings.Currency = "so'm";
            if (settings.CustomRows == null)
            {
                settings.CustomRows = new List<CustomRow>
                {
                    new CustomRow { Name = "Ish haqi (tayyorlash)", Unit = "ish" },
                    new CustomRow { Name = "Montaj", Unit = "ish" },
                };
            }
            return settings;
        }

        public void Save()
        {
            Directory.CreateDirectory(Folder);
            string temp = FilePath + ".tmp";
            using (FileStream stream = File.Create(temp))
                Serializer().WriteObject(stream, this);
            if (File.Exists(FilePath))
                File.Delete(FilePath);
            File.Move(temp, FilePath);
        }

        public ConstructionChoice ToChoice()
        {
            var choice = new ConstructionChoice
            {
                FomaxThickness = FomaxThickness > 0 ? FomaxThickness : 5,
                AcpThickness = AcpThickness > 0 ? AcpThickness : 4,
                PaintReturns = PaintReturns,
                SheetUnit = WholeSheets ? SheetUnit.WholeSheets : SheetUnit.SquareMeters
            };
            if (Enum.TryParse(Returns, out ReturnKind returns))
                choice.Returns = returns;
            if (Enum.TryParse(Back, out BackKind back))
                choice.Back = back;

            string[] profile = (Profile ?? "20x20").Split('x');
            if (profile.Length == 2 && int.TryParse(profile[0], out int a) && int.TryParse(profile[1], out int b) && a > 0 && b > 0)
            {
                choice.ProfileA = a;
                choice.ProfileB = b;
            }
            return choice;
        }

        private static DataContractJsonSerializer Serializer() =>
            new DataContractJsonSerializer(typeof(RasxodSettings), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
    }
}
