using System;
using System.Windows.Media;
using ReklamaMaket.Core;
using ReklamaMaket.Model;

namespace ReklamaMaket.Designs
{
    /// <summary>Bitta ustunli bilbord (3×6 m va kattaroq), tepasida projektorlar.</summary>
    public sealed class BillboardDesign : DesignType
    {
        private static readonly Color ConcreteColor = Color.FromRgb(0x9E, 0x9B, 0x96);
        private static readonly Color LampColor = Color.FromRgb(0x2E, 0x31, 0x35);

        public override string Name => "Bilbord";

        public override string Description =>
            "Ko'cha bo'yidagi bir ustunli bilbord. O'lcham — reklama yuzi (masalan 6×3 m), atrofida metall karkas, tepasida projektorlar.";

        public override SceneKind Scene => SceneKind.Street;

        public override Color DefaultFrameColor => Color.FromRgb(0x4B, 0x50, 0x57);

        public override SizePreset[] Presets => new[]
        {
            new SizePreset("6 × 3 m (standart)", 6000, 3000, 300, 4500),
            new SizePreset("12 × 4 m (superbord)", 12000, 4000, 400, 6000),
            new SizePreset("15 × 5 m (supersayt)", 15000, 5000, 500, 7000),
            new SizePreset("4 × 2 m", 4000, 2000, 250, 3000),
        };

        public override void Build(SignModel model)
        {
            MockupSettings s = model.Settings;
            double border = Num.Clamp(Math.Min(s.Width, s.Height) * 0.035, 60, 200);
            double poleSize = Num.Clamp(s.Width * 0.07, 300, 1200);
            double baseSize = poleSize * 2.2;
            double baseHeight = Num.Clamp(s.Elevation * 0.06, 150, 400);
            Color poleColor = Darker(s.FrameColor, 0.85);

            // Fundament va ustun karkas orqasida turadi
            model.Add(new Part("Fundament", PartKind.Base, -baseSize / 2, 0, -poleSize / 2 - baseSize / 2, baseSize, baseHeight, baseSize, ConcreteColor));
            model.Add(new Part("Ustun", PartKind.Pole, -poleSize / 2, 0, -poleSize, poleSize, s.Elevation + s.Height / 2, poleSize, poleColor));

            model.Add(new Part("Karkas", PartKind.Frame,
                -s.Width / 2 - border, s.Elevation - border, 0,
                s.Width + 2 * border, s.Height + 2 * border, s.Depth, s.FrameColor));

            model.Face = model.Add(new Part("Reklama yuzi", PartKind.Face, -s.Width / 2, s.Elevation, s.Depth, s.Width, s.Height, 10, s.FrameColor)
            {
                Glows = true
            });

            // Projektorlar: karkas tepasidan oldinga chiqib turadi
            int count = Math.Max(2, (int)Math.Round(s.Width / 2500));
            double lampWidth = Num.Clamp(s.Width * 0.05, 200, 600);
            double lampHeight = lampWidth * 0.35;
            double lampDepth = lampWidth * 0.6;
            double reach = Num.Clamp(s.Height * 0.2, 400, 1000);
            double rise = lampHeight * 1.2;
            double top = s.Elevation + s.Height + border;

            for (int i = 0; i < count; i++)
            {
                double cx = -s.Width / 2 + s.Width * (i + 0.5) / count;
                model.Add(new Part("Projektor ushlagichi", PartKind.Frame, cx - 20, top, s.Depth, 40, rise, reach, poleColor));
                model.Add(new Part("Projektor", PartKind.Lamp, cx - lampWidth / 2, top + rise, s.Depth + reach - lampDepth, lampWidth, lampHeight, lampDepth, LampColor)
                {
                    Glows = true
                });
            }
        }
    }
}
