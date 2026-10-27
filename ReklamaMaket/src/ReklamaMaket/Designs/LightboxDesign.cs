using System;
using System.Windows.Media;
using ReklamaMaket.Core;
using ReklamaMaket.Model;

namespace ReklamaMaket.Designs
{
    /// <summary>Fasadga osiladigan, ichidan yoritiladigan quti.</summary>
    public sealed class LightboxDesign : DesignType
    {
        public override string Name => "Laytboks";

        public override string Description =>
            "Fasadga osiladigan, ichidan yoritiladigan quti. Old tomoni — logo bosilgan yuz, atrofi — alyumin profil.";

        public override SceneKind Scene => SceneKind.Facade;

        public override Color DefaultFrameColor => Color.FromRgb(0xB8, 0xBD, 0xC2);

        public override SizePreset[] Presets => new[]
        {
            new SizePreset("3000 × 800 mm", 3000, 800, 120, 2800),
            new SizePreset("2000 × 600 mm", 2000, 600, 100, 2600),
            new SizePreset("4000 × 1000 mm", 4000, 1000, 150, 3000),
            new SizePreset("6000 × 1200 mm", 6000, 1200, 200, 3200),
            new SizePreset("1000 × 1000 mm (kvadrat)", 1000, 1000, 120, 2800),
        };

        public override void Build(SignModel model)
        {
            MockupSettings s = model.Settings;
            double profile = Math.Min(Num.Clamp(Math.Min(s.Width, s.Height) * 0.04, 15, 60), Math.Min(s.Width, s.Height) / 4);
            double x = -s.Width / 2;

            model.Add(new Part("Korpus (profil)", PartKind.Frame, x, s.Elevation, 0, s.Width, s.Height, s.Depth, s.FrameColor)
            {
                CastsShadow = true
            });

            model.Face = model.Add(new Part("Yuz", PartKind.Face,
                x + profile, s.Elevation + profile, s.Depth,
                s.Width - 2 * profile, s.Height - 2 * profile, 3, s.FrameColor)
            {
                Glows = true
            });
        }
    }
}
