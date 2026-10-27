using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using corel = Corel.Interop.VGCore;
using WpfColor = System.Windows.Media.Color;

namespace ReklamaMaket.Core
{
    /// <summary>Corel bilan ishlash: logoni olish, PNG eksport va rasm import.</summary>
    public sealed class CorelService
    {
        private const int PreviewPixels = 1000;
        private const double PointsPerMm = 72 / 25.4;

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

                BitmapSource preview = null;
                string file = null;
                try
                {
                    file = ExportSelection(doc, Math.Max(width, height), PreviewPixels, true);
                    preview = LoadBitmap(file);
                }
                catch (Exception ex)
                {
                    // Rasm chiqmasa ham logo ishlayveradi — panelda "LOGO" belgisi ko'rinadi
                    Debug.WriteLine("ReklamaMaket: logo preview eksport xatosi: " + ex);
                }
                finally
                {
                    TryDelete(file);
                }

                return new LogoInfo(selection, width, height, selection.Count, preview);
            }
            finally
            {
                doc.Unit = oldUnit;
            }
        }

        /// <summary>Obyektni PNG faylga eksport qiladi (ChatGPT'ga yuborish uchun). Fayl yo'lini qaytaradi.</summary>
        public string ExportShapePng(corel.Shape shape, int maxPixels)
        {
            corel.Document doc = RequireDocument();
            try
            {
                shape.CreateSelection();
            }
            catch (Exception ex)
            {
                throw new UserException("Dizayn topilmadi: u o'chirilgan yoki boshqa sahifada. Dizayn sahifasini oching yoki uni qayta chizing.", ex);
            }
            return ExportCurrentSelection(doc, maxPixels);
        }

        /// <summary>Corelda tanlangan obyektlarni PNG faylga eksport qiladi.</summary>
        public string ExportSelectionPng(int maxPixels)
        {
            corel.Document doc = RequireDocument();
            if (doc.SelectionRange.Count == 0)
                throw new UserException("Corelda hech narsa tanlanmagan. Dizaynni belgilang.");
            return ExportCurrentSelection(doc, maxPixels);
        }

        private static string ExportCurrentSelection(corel.Document doc, int maxPixels)
        {
            corel.cdrUnit oldUnit = doc.Unit;
            try
            {
                doc.Unit = corel.cdrUnit.cdrMillimeter;
                corel.ShapeRange selection = doc.SelectionRange;
                double maxSide = Math.Max(selection.SizeWidth, selection.SizeHeight);
                return ExportSelection(doc, maxSide, maxPixels, false);
            }
            finally
            {
                doc.Unit = oldUnit;
            }
        }

        /// <summary>Joriy tanlovni PNG ga eksport qiladi. Hujjat birligi mm bo'lishi kerak.</summary>
        private static string ExportSelection(corel.Document doc, double maxSideMm, int maxPixels, bool transparent)
        {
            string file = Path.Combine(TempFolder, "export_" + Guid.NewGuid().ToString("N") + ".png");
            int dpi = (int)Math.Round(maxPixels / (Math.Max(maxSideMm, 0.01) / 25.4));
            dpi = Math.Max(10, Math.Min(1200, dpi));

            corel.ExportFilter filter = doc.ExportBitmap(
                file,
                corel.cdrFilter.cdrPNG,
                corel.cdrExportRange.cdrSelection,
                corel.cdrImageType.cdrRGBColorImage,
                0, 0, dpi, dpi,
                corel.cdrAntiAliasingType.cdrNormalAntiAliasing,
                false,        // Dithered
                transparent);
            filter.Finish();

            if (!File.Exists(file))
                throw new UserException("Corel rasmni eksport qilmadi.");
            return file;
        }

        /// <summary>Rasmlarni (masalan ChatGPT natijasi) Corelga yonma-yon joylaydi.</summary>
        public void ImportImages(IList<string> files, IList<string> labels, string title, bool newPage)
        {
            corel.Document doc = RequireDocument();
            const double imageWidth = 200;
            const double gap = 15;
            const double textHeight = 6;

            corel.cdrUnit oldUnit = doc.Unit;
            doc.BeginCommandGroup("ChatGPT rasmlari");
            corel.ShapeRange created = app.CreateShapeRange();
            try
            {
                doc.Unit = corel.cdrUnit.cdrMillimeter;
                corel.Layer layer;
                double left, bottom;

                if (newPage)
                {
                    corel.Page page = doc.AddPagesEx(1, files.Count * imageWidth + (files.Count + 1) * gap, imageWidth + 2 * gap + textHeight * 6);
                    page.Activate();
                    TryRename(page, "ChatGPT ko'rinish");
                    layer = page.ActiveLayer;
                    left = page.CenterX - page.SizeWidth / 2;
                    bottom = page.CenterY - page.SizeHeight / 2;
                }
                else
                {
                    corel.Page page = doc.ActivePage;
                    layer = doc.ActiveLayer;
                    left = page.CenterX + page.SizeWidth / 2 + gap;
                    bottom = page.CenterY - page.SizeHeight / 2;
                }

                double tallest = 0;
                for (int i = 0; i < files.Count; i++)
                {
                    layer.Import(files[i]);
                    corel.ShapeRange imported = doc.SelectionRange;
                    if (imported == null || imported.Count == 0)
                        throw new UserException("Rasmni Corelga import qilib bo'lmadi: " + files[i]);

                    corel.Shape image = imported.Count > 1 ? imported.Group() : imported.FirstShape;
                    double height = imageWidth * image.SizeHeight / Math.Max(image.SizeWidth, 0.01);
                    tallest = Math.Max(tallest, height);
                    image.SetSize(imageWidth, height);
                    image.CenterX = left + gap + i * (imageWidth + gap) + imageWidth / 2;
                    image.CenterY = bottom + gap + textHeight * 2.5 + height / 2;
                    image.Name = labels[i];
                    created.Add(image);
                    created.Add(Text(layer, labels[i], image.CenterX, bottom + gap + textHeight, textHeight));
                }

                corel.Shape heading = Text(layer, title, 0, 0, textHeight * 1.3);
                heading.CenterX = left + gap + heading.SizeWidth / 2;
                heading.CenterY = bottom + gap + textHeight * 2.5 + tallest + textHeight * 2;
                created.Add(heading);

                corel.Shape group = created.Group();
                group.Name = "ChatGPT ko'rinish";
                group.CreateSelection();
            }
            catch
            {
                try
                {
                    if (created.Count > 0)
                        created.Delete();
                }
                catch
                {
                    // Qolgan obyektlarni Ctrl+Z bilan olib tashlash mumkin
                }
                throw;
            }
            finally
            {
                doc.Unit = oldUnit;
                doc.EndCommandGroup();
                app.Refresh();
            }

            try
            {
                app.ActiveWindow.ActiveView.ToFitSelection();
            }
            catch
            {
                // Zoom qilib bo'lmasa ham rasmlar joyida
            }
        }

        private corel.Shape Text(corel.Layer layer, string text, double centerX, double centerY, double heightMm)
        {
            corel.Shape shape = layer.CreateArtisticText(centerX, centerY, text);
            shape.Text.Story.Font = "Arial";
            shape.Text.Story.Size = (float)(heightMm * PointsPerMm);
            WpfColor ink = WpfColor.FromRgb(0x22, 0x22, 0x22);
            shape.Fill.UniformColor.RGBAssign(ink.R, ink.G, ink.B);
            shape.CenterX = centerX;
            shape.CenterY = centerY;
            return shape;
        }

        public static void TryRename(corel.Page page, string name)
        {
            try
            {
                page.Name = name;
            }
            catch
            {
                // Eski versiyalarda sahifa nomini o'zgartirish bo'lmasligi mumkin — muhim emas
            }
        }

        public static BitmapSource LoadBitmap(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
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
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
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
