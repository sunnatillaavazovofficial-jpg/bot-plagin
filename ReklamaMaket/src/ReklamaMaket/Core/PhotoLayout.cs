using System;

namespace ReklamaMaket.Core
{
    /// <summary>Belgining bino rasmidagi o'rni: 0..1 (rasm eni/bo'yiga nisbatan), chap-yuqori burchakdan.</summary>
    public struct PhotoPlacement
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public double CenterX => X + Width / 2;
        public double CenterY => Y + Height / 2;
    }

    /// <summary>
    /// Belgini rasm ustiga joylash. Panel ko'rinishi va Corel chizmasi shu bitta hisobdan foydalanadi.
    /// Belgi rasmdan chiqib ketmaydi.
    /// </summary>
    public static class PhotoLayout
    {
        public const double MinWidth = 0.05;

        public static PhotoPlacement Compute(SignSettings s) => Compute(s, s.PhotoX, s.PhotoY);

        /// <summary>Belgi markazini (centerX, centerY) ga qo'yganda qayerga tushishi.</summary>
        public static PhotoPlacement Compute(SignSettings s, double centerX, double centerY)
        {
            SignLayout layout = LogoLayout.Compute(s);
            double photoAspect = s.PhotoAspect > 0 ? s.PhotoAspect : 1.5;
            double signAspect = layout.BackgroundWidth / Math.Max(layout.BackgroundHeight, 1);

            double width = Num.Clamp(s.PhotoSignWidth, MinWidth, 1);
            double height = width * photoAspect / signAspect;
            if (height > 1)
            {
                width /= height;
                height = 1;
            }

            double x = Num.Clamp(centerX, width / 2, 1 - width / 2);
            double y = Num.Clamp(centerY, height / 2, 1 - height / 2);
            return new PhotoPlacement { X = x - width / 2, Y = y - height / 2, Width = width, Height = height };
        }
    }
}
