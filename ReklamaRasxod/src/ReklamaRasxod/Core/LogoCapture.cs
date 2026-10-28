using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ReklamaMaket.Core;
using corel = Corel.Interop.VGCore;

namespace ReklamaRasxod.Core
{
    /// <summary>Olingan logo: panel ko'rinishi uchun <see cref="LogoInfo"/> va o'lchov uchun <see cref="LogoShape"/>.</summary>
    public sealed class LogoCapture
    {
        /// <summary>O'lchov rasmining eng katta tomoni, piksel (kattaroq — aniqroq).</summary>
        private const int MeasurePixels = 3000;

        /// <summary>Paneldagi ko'rinish uchun rasm, piksel.</summary>
        private const int PreviewPixels = 1000;

        private static LogoCapture placeholder;

        private LogoCapture(LogoInfo info, LogoShape shape, bool opaque)
        {
            Info = info;
            Shape = shape;
            Opaque = opaque;
        }

        /// <summary>Panel ko'rinishi uchun (Shapes — Corel obyektlari; rasmdan olinganda null).</summary>
        public LogoInfo Info { get; }

        public LogoShape Shape { get; }

        /// <summary>Rasm shaffof emas edi — logo fon rangidan ajratib olindi (taxminiy).</summary>
        public bool Opaque { get; }

        /// <summary>Corelda tanlangan obyektlar: yuqori aniqlikda PNG eksport qilib o'lchanadi.</summary>
        public static LogoCapture FromSelection(CorelService service)
        {
            corel.Document doc = service.RequireDocument();
            corel.ShapeRange selection = doc.SelectionRange;
            if (selection == null || selection.Count == 0)
                throw new UserException("Logo tanlanmagan. Corelda logoni belgilang va qaytadan bosing.");

            corel.cdrUnit oldUnit = doc.Unit;
            string file = Path.Combine(CorelService.TempFolder, "rasxod_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                doc.Unit = corel.cdrUnit.cdrMillimeter;
                double width = Math.Max(selection.SizeWidth, 0.01);
                double height = Math.Max(selection.SizeHeight, 0.01);
                int dpi = (int)Math.Round(MeasurePixels / (Math.Max(width, height) / 25.4));
                dpi = Math.Max(10, Math.Min(1200, dpi));

                corel.ExportFilter filter = doc.ExportBitmap(
                    file,
                    corel.cdrFilter.cdrPNG,
                    corel.cdrExportRange.cdrSelection,
                    corel.cdrImageType.cdrRGBColorImage,
                    0, 0, dpi, dpi,
                    corel.cdrAntiAliasingType.cdrNormalAntiAliasing,
                    false,        // Dithered
                    true);        // Transparent
                filter.Finish();

                BitmapSource bitmap = CorelService.LoadBitmap(file);
                if (bitmap == null)
                    throw new UserException("Corel logoni rasmga eksport qilmadi — o'lchab bo'lmadi.");
                return FromBitmap(bitmap, width, height, selection.Count, selection);
            }
            finally
            {
                doc.Unit = oldUnit;
                CorelService.TryDelete(file);
            }
        }

        /// <summary>Tayyor rasmdan (masalan, Reklama Maket saqlagan logo).</summary>
        public static LogoCapture FromBitmap(BitmapSource bitmap, double widthMm, double heightMm, int count, corel.ShapeRange shapes)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            var pixels = new byte[width * height * 4];
            new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, width * 4, 0);

            var alpha = new byte[width * height];
            bool opaque = true;
            for (int i = 0; i < alpha.Length; i++)
            {
                alpha[i] = pixels[i * 4 + 3];
                if (alpha[i] < 250)
                    opaque = false;
            }
            if (opaque)
                AlphaFromBackground(pixels, alpha);

            var shape = new LogoShape(alpha, width, height);
            if (shape.IsEmpty)
                throw new UserException("Tanlangan obyektda logo topilmadi (rasm bo'sh chiqdi).");

            var info = new LogoInfo(shapes, widthMm, heightMm, count, Downscale(bitmap));
            return new LogoCapture(info, shape, opaque);
        }

        /// <summary>Logo tanlanmaganda: "LOGO" yozuvi (taxminiy hisob uchun).</summary>
        public static LogoCapture Placeholder()
        {
            if (placeholder != null)
                return placeholder;

            var text = new FormattedText("LOGO", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial Black"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal),
                200, Brushes.Black, 1.0);
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
                dc.DrawText(text, new Point(0, -text.Height * 0.1));

            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(text.Width), (int)Math.Ceiling(text.Height * 0.85), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            placeholder = FromBitmap(bitmap, bitmap.PixelWidth, bitmap.PixelHeight, 0, null);
            return placeholder;
        }

        /// <summary>Shaffof bo'lmagan rasm: burchakdagi rang — fon, undan farq qiluvchi piksellar — logo.</summary>
        private static void AlphaFromBackground(byte[] bgra, byte[] alpha)
        {
            int b0 = bgra[0], g0 = bgra[1], r0 = bgra[2];
            for (int i = 0; i < alpha.Length; i++)
            {
                int db = bgra[i * 4] - b0, dg = bgra[i * 4 + 1] - g0, dr = bgra[i * 4 + 2] - r0;
                double distance = Math.Sqrt(db * db + dg * dg + dr * dr);
                alpha[i] = (byte)Num.Clamp((distance - 20) * 255 / 40, 0, 255);
            }
        }

        private static BitmapSource Downscale(BitmapSource bitmap)
        {
            int longest = Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
            if (longest <= PreviewPixels)
                return bitmap;

            double scale = (double)PreviewPixels / longest;
            var small = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
            small.Freeze();
            return small;
        }
    }
}
