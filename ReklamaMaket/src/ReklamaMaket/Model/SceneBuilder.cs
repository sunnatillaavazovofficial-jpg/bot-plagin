using System;
using System.Linq;
using System.Windows.Media;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Model
{
    /// <summary>
    /// Dizayn turidan konstruksiyani oladi va unga atrof-muhit (devor, yer, osmon, soya)
    /// hamda masshtab uchun odam siluyetini qo'shadi.
    /// </summary>
    public static class SceneBuilder
    {
        public const double PersonHeight = 1750;

        private static readonly Color WallColor = Color.FromRgb(0xDD, 0xD0, 0xBA);
        private static readonly Color PlinthColor = Color.FromRgb(0x8F, 0x8A, 0x83);
        private static readonly Color PavementColor = Color.FromRgb(0xA4, 0xA4, 0xA2);
        private static readonly Color GrassColor = Color.FromRgb(0x86, 0xA8, 0x5E);
        private static readonly Color PersonColor = Color.FromRgb(0x3A, 0x3F, 0x47);

        public static SignModel Build(DesignType design, MockupSettings settings)
        {
            var model = new SignModel(design, settings);
            design.Build(model);

            if (model.Face == null)
                throw new InvalidOperationException(design.Name + ": yuz (Face) belgilanmagan.");

            Extents construction = model.ConstructionExtents();
            double front = model.Parts.Max(p => p.Front);

            if (settings.DrawEnvironment)
            {
                if (design.Scene == SceneKind.Facade)
                    AddFacade(model, construction);
                else
                    AddStreet(model, construction);
            }

            if (settings.DrawPerson)
            {
                double personWidth = 520;
                double x = construction.MinX - Math.Max(900, construction.Width * 0.08) - personWidth;
                model.Add(new Part("Odam (1.75 m)", PartKind.Person, x, 0, front + 400, personWidth, PersonHeight, 300, PersonColor));
            }

            return model;
        }

        private static void AddFacade(SignModel model, Extents c)
        {
            double margin = Math.Max(c.Width * 0.35, 2600);
            double wallTop = Math.Max(c.MaxY + Math.Max(c.Height * 0.5, 900), 3500);
            double left = c.MinX - margin;
            double width = c.Width + 2 * margin;

            // Soyalar devorga, konstruksiya qismlaridan oldin chiziladi
            foreach (Part part in model.Parts.Where(p => p.CastsShadow).ToList())
            {
                double offset = Num.Clamp(part.D * 0.7, 12, 250);
                model.Add(new Part("Soya", PartKind.Shadow, part.X + offset, part.Y - offset, 0, part.W, part.H, 0.1, Colors.Black));
            }

            model.Add(new Part("Devor", PartKind.Wall, left, 0, -300, width, wallTop, 300, WallColor));
            model.Add(new Part("Sokol", PartKind.Plinth, left, 0, -300, width, 450, 320, PlinthColor));
            model.Add(new Part("Trotuar", PartKind.Ground, left, -250, -300, width, 250, 3300, PavementColor));
        }

        private static void AddStreet(SignModel model, Extents c)
        {
            double margin = Math.Max(c.Width * 0.5, 3000);
            double left = c.MinX - margin;
            double width = c.Width + 2 * margin;

            // Old tomondan yer — pastdagi yashil tasma; soya shu tasma ichiga tushadi
            double groundHeight = Num.Clamp(c.Height * 0.12, 500, 2000);
            model.Add(new Part("Yer", PartKind.Ground, left, -groundHeight, -4000, width, groundHeight, 8000, GrassColor));

            double shadowWidth = Math.Max(c.Width * 0.8, 1500);
            double shadowHeight = Math.Min(groundHeight * 0.45, shadowWidth * 0.15);
            model.Add(new Part("Soya", PartKind.Shadow, c.CenterX - shadowWidth / 2, -shadowHeight * 0.75, 0, shadowWidth, shadowHeight, 0.1, Colors.Black)
            {
                Round = true
            });

            model.Sky = new Extents
            {
                MinX = left,
                MinY = 0,
                MaxX = left + width,
                MaxY = c.MaxY + Math.Max(c.Height * 0.35, 1200)
            };
        }
    }
}
