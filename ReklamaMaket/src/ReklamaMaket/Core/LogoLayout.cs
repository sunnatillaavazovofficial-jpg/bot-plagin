using System;
using System.Windows;

namespace ReklamaMaket.Core
{
    /// <summary>
    /// Logoni yuz (fon) ichiga joylashtirish hisobi. Panel ko'rinishi ham, Corelda chizish ham
    /// shu bitta hisobdan foydalanadi — shuning uchun ikkalasi bir xil chiqadi.
    /// </summary>
    public static class LogoLayout
    {
        /// <summary>
        /// Logo to'rtburchagini qaytaradi. Koordinatalar yuzning chap-yuqori burchagidan,
        /// Y pastga qarab o'sadi. Natija birligi faceWidth/faceHeight birligi bilan bir xil.
        /// </summary>
        public static Rect Place(double faceWidth, double faceHeight, double logoWidth, double logoHeight, MockupSettings settings)
        {
            logoWidth = Math.Max(logoWidth, 0.001);
            logoHeight = Math.Max(logoHeight, 0.001);

            double margin = Math.Min(faceWidth, faceHeight) * Num.Clamp(settings.MarginPercent, 0, 45) / 100.0;
            double availableWidth = Math.Max(faceWidth - 2 * margin, faceWidth * 0.05);
            double availableHeight = Math.Max(faceHeight - 2 * margin, faceHeight * 0.05);

            double fit = Math.Min(availableWidth / logoWidth, availableHeight / logoHeight);
            fit *= Num.Clamp(settings.LogoSizePercent, 5, 100) / 100.0;

            double width = logoWidth * fit;
            double height = logoHeight * fit;
            double x = (faceWidth - width) / 2;
            double y = (faceHeight - height) / 2;

            switch (settings.Align)
            {
                case LogoAlign.Left:
                    x = margin;
                    break;
                case LogoAlign.Right:
                    x = faceWidth - margin - width;
                    break;
                case LogoAlign.Top:
                    y = margin;
                    break;
                case LogoAlign.Bottom:
                    y = faceHeight - margin - height;
                    break;
            }

            return new Rect(x, y, width, height);
        }
    }
}
