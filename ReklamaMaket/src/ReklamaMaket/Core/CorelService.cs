using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using corel = Corel.Interop.VGCore;

namespace ReklamaMaket.Core
{
    /// <summary>Corel bilan ishlash: logoni olish va uning rasmini (preview) eksport qilish.</summary>
    public sealed class CorelService
    {
        private const int PreviewPixels = 1000;

        private readonly corel.Application app;

        public CorelService(corel.Application app)
        {
            this.app = app;
        }

        public static string TempFolder
        {
            get
            {
                string folder = Path.Combine(Path.GetTempPath(), "ReklamaMaket");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public corel.Document RequireDocument()
        {
            if (app == null)
                throw new UserException("CorelDRAW bilan bog'lanib bo'lmadi.");

            corel.Document doc = app.ActiveDocument;
            if (doc == null)
                throw new UserException("Ochiq hujjat yo'q. Avval CorelDRAW'da fayl oching.");
            return doc;
        }

        public bool HasSelection()
        {
            try
            {
                corel.Document doc = app?.ActiveDocument;
                return doc != null && doc.SelectionRange.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Hozir tanlangan obyekt(lar)ni logo sifatida oladi.</summary>
        public LogoInfo CaptureSelection()
        {
            corel.Document doc = RequireDocument();
            corel.ShapeRange selection = doc.SelectionRange;
            if (selection == null || selection.Count == 0)
                throw new UserException("Logo tanlanmagan. Corelda logoni belgilang va qaytadan bosing.");

            corel.cdrUnit oldUnit = doc.Unit;
            try
            {
                doc.Unit = corel.cdrUnit.cdrMillimeter;
                double width = Math.Max(selection.SizeWidth, 0.01);
                double height = Math.Max(selection.SizeHeight, 0.01);
                BitmapSource preview = TryExportPreview(doc, selection, Math.Max(width, height));
                return new LogoInfo(selection, width, height, selection.Count, preview);
            }
            finally
            {
                doc.Unit = oldUnit;
            }
        }

        private static BitmapSource TryExportPreview(corel.Document doc, corel.ShapeRange shapes, double maxSideMm)
        {
            string file = Path.Combine(TempFolder, "logo_" + DateTime.Now.Ticks + ".png");
            try
            {
                int dpi = (int)Math.Round(PreviewPixels / (maxSideMm / 25.4));
                dpi = Math.Max(10, Math.Min(1200, dpi));

                shapes.CreateSelection();
                corel.ExportFilter filter = doc.ExportBitmap(
                    file,
                    corel.cdrFilter.cdrPNG,
                    corel.cdrExportRange.cdrSelection,
                    corel.cdrImageType.cdrRGBColorImage,
                    0, 0, dpi, dpi,
                    corel.cdrAntiAliasingType.cdrNormalAntiAliasing,
                    false,  // Dithered
                    true);  // Transparent
                filter.Finish();

                return LoadBitmap(file);
            }
            catch (Exception ex)
            {
                // Rasm chiqmasa ham logo ishlayveradi — panelda "LOGO" belgisi ko'rinadi
                Debug.WriteLine("ReklamaMaket: logo preview eksport xatosi: " + ex);
                return null;
            }
            finally
            {
                TryDelete(file);
            }
        }

        public static BitmapSource LoadBitmap(string file)
        {
            if (!File.Exists(file))
                return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(file);
            image.EndInit();
            image.Freeze();
            return image;
        }

        public static void TryDelete(string file)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
