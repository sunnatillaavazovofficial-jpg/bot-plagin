using System;
using System.Windows.Media;
using ReklamaMaket.Core;
using ReklamaMaket.Model;

namespace ReklamaMaket.Designs
{
    /// <summary>Yerda turadigan vertikal pilon (stela).</summary>
    public sealed class PylonDesign : DesignType
    {
        private static readonly Color ConcreteColor = Color.FromRgb(0x9E, 0x9B, 0x96);

        public override string Name => "Pilon (stela)";

        public override string Description =>
            "Yerda postament ustida turadigan vertikal konstruksiya. Yuzi ichidan yoritiladi.";

        public override SceneKind Scene => SceneKind.Street;

        public override Color DefaultFrameColor => Color.FromRgb(0x3C, 0x41, 0x48);

        public override SizePreset[] Presets => new[]
        {
            new SizePreset("1200 × 4000 mm", 1200, 4000, 400, 300),
            new SizePreset("1000 × 3000 mm", 1000, 3000, 300, 250),
            new SizePreset("1500 × 6000 mm", 1500, 6000, 500, 400),
            new SizePreset("2000 × 8000 mm", 2000, 8000, 600, 500),
        };

        public override void Build(SignModel model)
        {
            MockupSettings s = model.Settings;
            double x = -s.Width / 2;

            if (s.Elevation >= 20)
            {
                model.Add(new Part("Postament", PartKind.Base, x - 100, 0, -100, s.Width + 200, s.Elevation, s.Depth + 200, ConcreteColor));
            }

            model.Add(new Part("Korpus", PartKind.Frame, x, s.Elevation, 0, s.Width, s.Height, s.Depth, s.FrameColor));

            double profile = Math.Min(Num.Clamp(Math.Min(s.Width, s.Height) * 0.05, 30, 120), Math.Min(s.Width, s.Height) / 4);
            model.Face = model.Add(new Part("Yuz", PartKind.Face,
                x + profile, s.Elevation + profile, s.Depth,
                s.Width - 2 * profile, s.Height - 2 * profile, 3, s.FrameColor)
            {
                Glows = true
            });
        }
    }
}
