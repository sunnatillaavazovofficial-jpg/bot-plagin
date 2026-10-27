using System;
using System.Windows.Media;
using ReklamaMaket.Core;
using ReklamaMaket.Model;

namespace ReklamaMaket.Designs
{
    /// <summary>Devorga luvers orqali tortilgan banner.</summary>
    public sealed class BannerDesign : DesignType
    {
        private const double EyeletStep = 500;

        private static readonly Color EyeletColor = Color.FromRgb(0x8E, 0x92, 0x98);

        public override string Name => "Banner";

        public override string Description =>
            "Devorga tortilgan banner. Chetlari bo'ylab har ~50 sm da luvers (temir halqa) qo'yiladi.";

        public override SceneKind Scene => SceneKind.Facade;

        public override Color DefaultFrameColor => Color.FromRgb(0xE6, 0xE6, 0xE6);

        public override SizePreset[] Presets => new[]
        {
            new SizePreset("6000 × 3000 mm", 6000, 3000, 5, 1500),
            new SizePreset("3000 × 2000 mm", 3000, 2000, 5, 1500),
            new SizePreset("2000 × 1000 mm", 2000, 1000, 5, 1600),
            new SizePreset("10000 × 5000 mm", 10000, 5000, 5, 2000),
        };

        public override void Build(SignModel model)
        {
            MockupSettings s = model.Settings;
            double x = -s.Width / 2;
            double y = s.Elevation;
            double depth = Math.Max(s.Depth, 2);

            model.Face = model.Add(new Part("Banner", PartKind.Face, x, y, 0, s.Width, s.Height, depth, s.FrameColor)
            {
                CastsShadow = true
            });

            double inset = Num.Clamp(Math.Min(s.Width, s.Height) * 0.015, 15, 40);
            double size = Num.Clamp(Math.Min(s.Width, s.Height) * 0.012, 12, 40);

            int columns = Math.Max(2, (int)Math.Round((s.Width - 2 * inset) / EyeletStep) + 1);
            int rows = Math.Max(2, (int)Math.Round((s.Height - 2 * inset) / EyeletStep) + 1);

            for (int i = 0; i < columns; i++)
            {
                double cx = x + inset + i * (s.Width - 2 * inset) / (columns - 1);
                AddEyelet(model, cx, y + inset, size, depth);
                AddEyelet(model, cx, y + s.Height - inset, size, depth);
            }

            // Burchaklar yuqorida qo'shildi, shuning uchun birinchi va oxirgi qator tashlab ketiladi
            for (int j = 1; j < rows - 1; j++)
            {
                double cy = y + inset + j * (s.Height - 2 * inset) / (rows - 1);
                AddEyelet(model, x + inset, cy, size, depth);
                AddEyelet(model, x + s.Width - inset, cy, size, depth);
            }
        }

        private static void AddEyelet(SignModel model, double cx, double cy, double size, double depth)
        {
            model.Add(new Part("Luvers", PartKind.Eyelet, cx - size / 2, cy - size / 2, depth, size, size, 2, EyeletColor)
            {
                Round = true
            });
        }
    }
}
