using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using corel = Corel.Interop.VGCore;

namespace ReklamaMaket.Core
{
    /// <summary>Logo orqasida turgan bir xil rangli katak — fon sifatida olinadi.</summary>
    public sealed class BackdropInfo
    {
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public Color Color { get; set; }
    }

    /// <summary>Corelda tanlab olingan logo.</summary>
    public sealed class LogoInfo
    {
        public LogoInfo(corel.ShapeRange shapes, double widthMm, double heightMm, int count, BitmapSource preview,
            IList<Color> colors = null, BackdropInfo backdrop = null)
        {
            Shapes = shapes;
            WidthMm = widthMm;
            HeightMm = heightMm;
            Count = count;
            Preview = preview;
            Colors = colors ?? new List<Color>();
            Backdrop = backdrop;
        }

        /// <summary>Logoning asl obyektlari (ular o'zgartirilmaydi, faqat nusxasi olinadi). Katak bo'lsa — u ham shu yerda.</summary>
        public corel.ShapeRange Shapes { get; }

        /// <summary>Logoning o'zi (katak bo'lsa — uning ichidagi qism) o'lchami, mm.</summary>
        public double WidthMm { get; }
        public double HeightMm { get; }
        public int Count { get; }

        /// <summary>Panelda ko'rsatish uchun PNG rasm (katak olib tashlangan). Eksport bo'lmasa null.</summary>
        public BitmapSource Preview { get; }

        /// <summary>Logoning asosiy ranglari, ko'pi birinchi.</summary>
        public IList<Color> Colors { get; }

        /// <summary>Orqadagi katak; yo'q bo'lsa null.</summary>
        public BackdropInfo Backdrop { get; }
    }
}
