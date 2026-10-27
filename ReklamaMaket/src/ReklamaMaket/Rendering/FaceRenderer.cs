using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReklamaMaket.Core;
using ReklamaMaket.Model;

namespace ReklamaMaket.Rendering
{
    /// <summary>
    /// Yuzning rasmi: fon + logo. Paneldagi 2D ko'rinishda ham, 3D modelda tekstura sifatida ham ishlatiladi.
    /// </summary>
    public static class FaceRenderer
    {
        public static BitmapSource Render(SignModel model, LogoInfo logo, int maxPixels = 1024)
        {
            Part face = model.Face;
            double aspect = face.W / face.H;
            int width = aspect >= 1 ? maxPixels : Math.Max(16, (int)Math.Round(maxPixels * aspect));
            int height = aspect >= 1 ? Math.Max(16, (int)Math.Round(maxPixels / aspect)) : maxPixels;

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(model.Settings.Background), null, new Rect(0, 0, width, height));

                double logoWidth = logo != null ? logo.WidthMm : 2;
                double logoHeight = logo != null ? logo.HeightMm : 1;
                Rect place = LogoLayout.Place(width, height, logoWidth, logoHeight, model.Settings);

                if (logo?.Preview != null)
                    dc.DrawImage(logo.Preview, place);
                else
                    DrawPlaceholder(dc, place, model.Settings.Background);
            }

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static void DrawPlaceholder(DrawingContext dc, Rect place, Color background)
        {
            bool dark = background.R * 0.299 + background.G * 0.587 + background.B * 0.114 < 128;
            Brush ink = new SolidColorBrush(dark ? Color.FromArgb(170, 255, 255, 255) : Color.FromArgb(150, 0, 0, 0));
            var pen = new Pen(ink, Math.Max(1, place.Height * 0.02)) { DashStyle = DashStyles.Dash };
            dc.DrawRectangle(null, pen, place);

            var text = new FormattedText("LOGO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                Math.Max(6, place.Height * 0.35), ink, 1.0);
            dc.DrawText(text, new Point(place.X + (place.Width - text.Width) / 2, place.Y + (place.Height - text.Height) / 2));
        }
    }
}
