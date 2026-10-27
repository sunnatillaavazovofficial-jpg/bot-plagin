using System.Windows.Media.Imaging;
using corel = Corel.Interop.VGCore;

namespace ReklamaMaket.Core
{
    /// <summary>Corelda tanlab olingan logo.</summary>
    public sealed class LogoInfo
    {
        public LogoInfo(corel.ShapeRange shapes, double widthMm, double heightMm, int count, BitmapSource preview)
        {
            Shapes = shapes;
            WidthMm = widthMm;
            HeightMm = heightMm;
            Count = count;
            Preview = preview;
        }

        /// <summary>Logoning asl obyektlari (ular o'zgartirilmaydi, faqat nusxasi olinadi).</summary>
        public corel.ShapeRange Shapes { get; }

        public double WidthMm { get; }
        public double HeightMm { get; }
        public int Count { get; }

        /// <summary>Panelda ko'rsatish uchun PNG rasm. Eksport bo'lmasa null.</summary>
        public BitmapSource Preview { get; }
    }
}
