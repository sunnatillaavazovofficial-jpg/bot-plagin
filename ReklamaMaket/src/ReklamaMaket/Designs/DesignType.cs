using System.Windows.Media;
using ReklamaMaket.Model;

namespace ReklamaMaket.Designs
{
    /// <summary>Dizayn turining tayyor o'lchami (mm).</summary>
    public sealed class SizePreset
    {
        public SizePreset(string name, double width, double height, double depth, double elevation)
        {
            Name = name;
            Width = width;
            Height = height;
            Depth = depth;
            Elevation = elevation;
        }

        public string Name { get; }
        public double Width { get; }
        public double Height { get; }
        public double Depth { get; }
        public double Elevation { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Tashqi reklama turi (laytboks, banner, bilbord...). Yangi tur qo'shish uchun shu klassdan
    /// meros oling va <see cref="DesignCatalog"/> ro'yxatiga qo'shing.
    /// </summary>
    public abstract class DesignType
    {
        /// <summary>Paneldagi nomi.</summary>
        public abstract string Name { get; }

        /// <summary>Panelda nom ostida chiqadigan qisqa izoh.</summary>
        public abstract string Description { get; }

        /// <summary>Fasadda (devorda) yoki ko'chada (yerda) turadi.</summary>
        public abstract SceneKind Scene { get; }

        /// <summary>Tayyor o'lchamlar. Birinchisi — standart.</summary>
        public abstract SizePreset[] Presets { get; }

        public virtual Color DefaultBackground => Colors.White;

        public abstract Color DefaultFrameColor { get; }

        /// <summary>
        /// Konstruksiya qismlarini modelga qo'shadi (o'lchamlar <c>model.Settings</c> da, mm).
        /// Logo turadigan qismni <c>model.Face</c> ga yozish shart.
        /// Konstruksiya gorizontal bo'yicha X=0 atrofida markazlashadi, yer sathi Y=0.
        /// </summary>
        public abstract void Build(SignModel model);

        public override string ToString() => Name;

        protected static Color Darker(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)(color.R * factor),
                (byte)(color.G * factor),
                (byte)(color.B * factor));
        }
    }
}
