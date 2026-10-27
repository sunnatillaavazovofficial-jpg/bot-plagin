using System;
using System.Windows;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Core
{
    /// <summary>Fon o'lchami va logoning fon ichidagi o'rni (mm).</summary>
    public struct SignLayout
    {
        public double BackgroundWidth;
        public double BackgroundHeight;

        /// <summary>Logo to'rtburchagi: fonning chap-yuqori burchagidan, Y pastga qarab.</summary>
        public Rect Logo;
    }

    /// <summary>
    /// Logo va fon joylashuvi. Panel ko'rinishi ham, Corel chizmasi ham shu bitta hisobdan
    /// foydalanadi — shuning uchun ikkalasi bir xil chiqadi.
    /// </summary>
    public static class LogoLayout
    {
        public static SignLayout Compute(SignSettings s)
        {
            double margin = Math.Max(0, s.Margin);
            double width, height;

            if (s.AutoBackgroundSize || s.Background.Kind == BackgroundKind.Wall)
            {
                width = s.LogoWidth + 2 * margin;
                height = s.LogoHeight + 2 * margin;
            }
            else
            {
                width = Math.Max(s.BackgroundWidth, 1);
                height = Math.Max(s.BackgroundHeight, 1);
            }

            double x = (width - s.LogoWidth) / 2;
            double y = (height - s.LogoHeight) / 2;
            switch (s.Align)
            {
                case LogoAlign.Left:
                    x = margin;
                    break;
                case LogoAlign.Right:
                    x = width - margin - s.LogoWidth;
                    break;
                case LogoAlign.Top:
                    y = margin;
                    break;
                case LogoAlign.Bottom:
                    y = height - margin - s.LogoHeight;
                    break;
            }

            return new SignLayout
            {
                BackgroundWidth = width,
                BackgroundHeight = height,
                Logo = new Rect(x, y, s.LogoWidth, s.LogoHeight)
            };
        }
    }
}
