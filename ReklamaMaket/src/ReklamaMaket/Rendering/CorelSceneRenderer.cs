using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ReklamaMaket.Core;
using ReklamaMaket.Model;
using corel = Corel.Interop.VGCore;
using WpfColor = System.Windows.Media.Color;

namespace ReklamaMaket.Rendering
{
    /// <summary>
    /// Maketni CorelDRAW hujjatiga vektor obyektlar sifatida chizadi: atrof-muhit, konstruksiya,
    /// fon + logo nusxasi, odam siluyeti, o'lcham chiziqlari va sarlavha. Hammasi bitta guruhga yig'iladi
    /// va bitta "Undo" (Ctrl+Z) bilan bekor qilinadi.
    /// </summary>
    public sealed class CorelSceneRenderer
    {
        private const double PointsPerMm = 72 / 25.4;
        private const double MaxPageSize = 45000;

        private static readonly WpfColor InkColor = WpfColor.FromRgb(0x22, 0x22, 0x22);
        private static readonly WpfColor SkyTop = WpfColor.FromRgb(0x8E, 0xC1, 0xEA);
        private static readonly WpfColor SkyHorizon = WpfColor.FromRgb(0xEE, 0xF6, 0xFC);

        private readonly corel.Application app;
        private corel.Layer layer;
        private corel.ShapeRange created;

        private double k;          // model mm -> sahifa mm (masshtab)
        private double ox, oy;     // model (0, 0) nuqtasining sahifadagi o'rni
        private double textHeight; // sahifadagi yozuv balandligi, mm

        public CorelSceneRenderer(corel.Application app)
        {
            this.app = app;
        }

        /// <summary>Sahifadagi joy (model koordinatasi * masshtab, nol nuqtaga nisbatan).</summary>
        private struct Layout
        {
            public double MinX;
            public double MinY;
            public double MaxX;
            public double MaxY;
            public double TitleY;
        }

        public corel.Shape Draw(corel.Document doc, SignModel model, LogoInfo logo)
        {
            MockupSettings s = model.Settings;
            k = 1.0 / Math.Max(1, s.Scale);

            corel.cdrUnit oldUnit = doc.Unit;
            corel.Shape group = null;
            doc.BeginCommandGroup("Reklama maket");
            try
            {
                app.Optimization = true;
                app.EventsEnabled = false;
                doc.Unit = corel.cdrUnit.cdrMillimeter;
                Layout layout = ComputeLayout(model);
                Place(doc, model, logo, layout);
                created = app.CreateShapeRange();

                DrawSky(model);
                foreach (Part part in DrawOrder(model.Parts))
                    DrawPart(part, s, logo);

                if (s.DrawDimensions)
                    DrawDimensions(model);
                DrawTitle(model, layout);

                group = created.Group();
                group.Name = "Reklama maket - " + model.Design.Name;
            }
            catch
            {
                DeleteCreated();
                throw;
            }
            finally
            {
                doc.Unit = oldUnit;
                app.EventsEnabled = true;
                app.Optimization = false;
                doc.EndCommandGroup();
                app.Refresh();
            }

            ZoomTo(group);
            return group;
        }

        /// <summary>3D ko'rinish rasmlarini (PNG) Corelga yonma-yon joylaydi.</summary>
        public void ImportViews(corel.Document doc, IList<string> files, IList<string> labels, double aspect, bool newPage, string title)
        {
            const double imageWidth = 160;
            const double gap = 12;
            double imageHeight = imageWidth / Math.Max(aspect, 0.1);
            textHeight = 5;

            corel.cdrUnit oldUnit = doc.Unit;
            corel.Shape group = null;
            doc.BeginCommandGroup("3D ko'rinishlar");
            try
            {
                doc.Unit = corel.cdrUnit.cdrMillimeter;

                double left, bottom;
                if (newPage)
                {
                    double pageWidth = files.Count * imageWidth + (files.Count + 1) * gap;
                    double pageHeight = imageHeight + 2 * gap + textHeight * 6;
                    corel.Page page = doc.AddPagesEx(1, pageWidth, pageHeight);
                    page.Activate();
                    TryRenamePage(page, "3D ko'rinish");
                    layer = page.ActiveLayer;
                    left = page.LeftX;
                    bottom = page.BottomY;
                }
                else
                {
                    corel.Page page = doc.ActivePage;
                    layer = doc.ActiveLayer;
                    left = page.RightX + gap;
                    bottom = page.BottomY;
                }

                created = app.CreateShapeRange();
                double imageCenterY = bottom + gap + textHeight * 2.5 + imageHeight / 2;

                for (int i = 0; i < files.Count; i++)
                {
                    layer.Import(files[i]);
                    corel.ShapeRange imported = doc.SelectionRange;
                    if (imported == null || imported.Count == 0)
                        throw new UserException("Rasmni Corelga import qilib bo'lmadi: " + files[i]);

                    corel.Shape image = imported.Count > 1 ? imported.Group() : imported.FirstShape;
                    double centerX = left + gap + i * (imageWidth + gap) + imageWidth / 2;
                    image.SetSize(imageWidth, imageHeight);
                    image.CenterX = centerX;
                    image.CenterY = imageCenterY;
                    image.Name = labels[i];
                    created.Add(image);
                    created.Add(Label(labels[i], centerX, bottom + gap + textHeight, 0));
                }

                corel.Shape heading = layer.CreateArtisticText(left + gap, imageCenterY + imageHeight / 2 + textHeight * 1.2, title);
                StyleText(heading, textHeight * 1.3);
                PlaceLeftBottom(heading, left + gap, imageCenterY + imageHeight / 2 + textHeight * 1.2);
                created.Add(heading);

                group = created.Group();
                group.Name = "3D ko'rinishlar";
            }
            catch
            {
                DeleteCreated();
                throw;
            }
            finally
            {
                doc.Unit = oldUnit;
                doc.EndCommandGroup();
                app.Refresh();
            }

            ZoomTo(group);
        }

        // ---------------------------------------------------------------- joylashuv

        private Layout ComputeLayout(SignModel model)
        {
            MockupSettings s = model.Settings;
            Extents scene = model.SceneExtents();
            Extents construction = model.ConstructionExtents();

            textHeight = Num.Clamp(Math.Max(scene.Width, scene.Height) * k * 0.022, 2.5, 80);

            var layout = new Layout
            {
                MinX = scene.MinX * k,
                MinY = scene.MinY * k,
                MaxX = scene.MaxX * k,
                MaxY = scene.MaxY * k
            };

            if (s.DrawDimensions)
            {
                // DrawDimensions dagi chiziqlar bilan bir xil masofalar
                layout.MaxY = Math.Max(layout.MaxY, construction.MaxY * k + textHeight * 2.6);
                layout.MaxX = Math.Max(layout.MaxX, construction.MaxX * k + textHeight * 3.2);
                if (s.Elevation > 0)
                    layout.MinX = Math.Min(layout.MinX, construction.MinX * k - textHeight * 3.2);
            }

            layout.TitleY = layout.MaxY + textHeight * 1.2;
            layout.MaxY = layout.TitleY + textHeight * 1.8;

            // Tor konstruksiyada sarlavha sahifadan chiqib ketmasin (taxminiy matn eni)
            double titleWidth = TitleText(model).Length * textHeight * 1.3 * 0.6;
            layout.MaxX = Math.Max(layout.MaxX, layout.MinX + titleWidth);
            return layout;
        }

        private void Place(corel.Document doc, SignModel model, LogoInfo logo, Layout layout)
        {
            double width = layout.MaxX - layout.MinX;
            double height = layout.MaxY - layout.MinY;
            double pad = Math.Max(10, Math.Max(width, height) * 0.04);

            if (model.Settings.NewPage)
            {
                corel.Page page = doc.AddPagesEx(1,
                    Num.Clamp(width + 2 * pad, 10, MaxPageSize),
                    Num.Clamp(height + 2 * pad, 10, MaxPageSize));
                page.Activate();
                TryRenamePage(page, model.Design.Name);
                layer = page.ActiveLayer;
                ox = page.LeftX + pad - layout.MinX;
                oy = page.BottomY + pad - layout.MinY;
                return;
            }

            layer = doc.ActiveLayer;
            double anchorX, anchorY;
            try
            {
                // Logoning o'ng tomoniga
                anchorX = logo.Shapes.RightX + pad;
                anchorY = logo.Shapes.BottomY;
            }
            catch
            {
                corel.Page page = doc.ActivePage;
                anchorX = page.LeftX + pad;
                anchorY = page.BottomY + pad;
            }
            ox = anchorX - layout.MinX;
            oy = anchorY - layout.MinY;
        }

        private static void TryRenamePage(corel.Page page, string name)
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

        private static IEnumerable<Part> DrawOrder(IEnumerable<Part> parts)
        {
            // Avval devor/yer, keyin soyalar, keyin konstruksiya orqadan oldinga qarab
            return parts
                .Select((part, index) => new { part, index })
                .OrderBy(x => Rank(x.part.Kind))
                .ThenBy(x => x.part.Front)
                .ThenBy(x => x.index)
                .Select(x => x.part);
        }

        private static int Rank(PartKind kind)
        {
            switch (kind)
            {
                case PartKind.Wall:
                    return 0;
                case PartKind.Plinth:
                case PartKind.Ground:
                    return 1;
                case PartKind.Shadow:
                    return 2;
                default:
                    return 3;
            }
        }

        // ---------------------------------------------------------------- chizish

        private void DrawSky(SignModel model)
        {
            if (model.Sky.IsEmpty)
                return;

            Extents sky = model.Sky;
            corel.Shape shape = RectShape(sky.MinX, sky.MinY, sky.Width, sky.Height, 0);
            shape.Fill.ApplyFountainFill(Rgb(SkyHorizon), Rgb(SkyTop), corel.cdrFountainFillType.cdrLinearFountainFill, 90);
            shape.Outline.SetNoOutline();
            shape.Name = "Osmon";
            created.Add(shape);
        }

        private void DrawPart(Part part, MockupSettings settings, LogoInfo logo)
        {
            switch (part.Kind)
            {
                case PartKind.Person:
                    DrawPerson(part);
                    return;

                case PartKind.Face:
                    DrawFace(part, settings, logo);
                    return;

                case PartKind.Shadow:
                {
                    corel.Shape shadow = PartShape(part);
                    Fill(shadow, part.Color);
                    shadow.Transparency.ApplyUniformTransparency(part.Round ? 65 : 72);
                    Add(shadow, part.Name);
                    return;
                }

                case PartKind.Frame:
                case PartKind.Pole:
                case PartKind.Lamp:
                {
                    corel.Shape metal = PartShape(part);
                    metal.Fill.ApplyFountainFill(Rgb(Shade(part.Color, 0.78)), Rgb(Shade(part.Color, 1.15)), corel.cdrFountainFillType.cdrLinearFountainFill, 90);
                    metal.Outline.SetNoOutline();
                    Add(metal, part.Name);
                    return;
                }

                default:
                {
                    corel.Shape shape = PartShape(part);
                    Fill(shape, part.Color);
                    Add(shape, part.Name);
                    return;
                }
            }
        }

        /// <summary>Yuz: fon to'rtburchagi + logo nusxasi (o'lchamga moslab, markazga/chetga joylanadi).</summary>
        private void DrawFace(Part face, MockupSettings settings, LogoInfo logo)
        {
            corel.Shape background = PartShape(face);
            Fill(background, settings.Background);
            Add(background, "Fon (logo orqasi)");

            if (logo == null)
                return;

            Rect place = LogoLayout.Place(face.W, face.H, logo.WidthMm, logo.HeightMm, settings);
            corel.Shape copy = DuplicateLogo(logo);
            copy.SetSize(place.Width * k, place.Height * k);
            copy.CenterX = ox + (face.X + place.X + place.Width / 2) * k;
            copy.CenterY = oy + (face.Top - place.Y - place.Height / 2) * k;
            copy.OrderFrontOf(background);
            Add(copy, "Logo");
        }

        private corel.Shape DuplicateLogo(LogoInfo logo)
        {
            corel.ShapeRange copies;
            try
            {
                copies = logo.Shapes.Duplicate(0, 0);
            }
            catch (Exception ex)
            {
                throw new UserException("Logo topilmadi (o'chirilgan bo'lishi mumkin). Logoni qayta tanlab, \"Logoni olish\" tugmasini bosing.", ex);
            }

            corel.Shape shape = copies.Count > 1 ? copies.Group() : copies.FirstShape;
            shape.MoveToLayer(layer);
            return shape;
        }

        private void DrawPerson(Part p)
        {
            double head = p.H * 0.13;
            corel.Shape[] pieces =
            {
                RectShape(p.X + p.W * 0.2, p.Y, p.W * 0.26, p.H * 0.47, p.W * 0.08),         // chap oyoq
                RectShape(p.X + p.W * 0.54, p.Y, p.W * 0.26, p.H * 0.47, p.W * 0.08),        // o'ng oyoq
                RectShape(p.X + p.W * 0.08, p.Y + p.H * 0.43, p.W * 0.84, p.H * 0.42, p.W * 0.2), // tana
                layer.CreateEllipse2(ox + (p.X + p.W / 2) * k, oy + (p.Top - head / 2) * k, head * 0.42 * k, head / 2 * k) // bosh
            };

            corel.ShapeRange body = app.CreateShapeRange();
            foreach (corel.Shape piece in pieces)
            {
                Fill(piece, p.Color);
                body.Add(piece);
            }

            Add(body.Group(), p.Name);
        }

        private void DrawDimensions(SignModel model)
        {
            MockupSettings s = model.Settings;
            Extents construction = model.ConstructionExtents();
            corel.ShapeRange dims = app.CreateShapeRange();

            double x0 = ox - s.Width / 2 * k;
            double x1 = ox + s.Width / 2 * k;
            double yBottom = oy + s.Elevation * k;
            double yTop = oy + (s.Elevation + s.Height) * k;
            double tick = textHeight * 0.45;

            // Eni — konstruksiya tepasida
            double hy = oy + construction.MaxY * k + textHeight * 1.2;
            dims.Add(Line(x0, yTop, x0, hy + tick));
            dims.Add(Line(x1, yTop, x1, hy + tick));
            dims.Add(Line(x0, hy, x1, hy));
            dims.Add(Tick(x0, hy, tick));
            dims.Add(Tick(x1, hy, tick));
            dims.Add(Label(Num.Format(s.Width) + " mm", (x0 + x1) / 2, hy + textHeight * 0.9, 0));

            // Bo'yi — o'ng tomonda
            double vx = ox + construction.MaxX * k + textHeight * 1.2;
            dims.Add(Line(x1, yBottom, vx + tick, yBottom));
            dims.Add(Line(x1, yTop, vx + tick, yTop));
            dims.Add(Line(vx, yBottom, vx, yTop));
            dims.Add(Tick(vx, yBottom, tick));
            dims.Add(Tick(vx, yTop, tick));
            dims.Add(Label(Num.Format(s.Height) + " mm", vx + textHeight * 0.9, (yBottom + yTop) / 2, 90));

            // Yerdan balandligi — chap tomonda
            if (s.Elevation > 0)
            {
                double ex = ox + construction.MinX * k - textHeight * 1.2;
                dims.Add(Line(ex - tick, yBottom, x0, yBottom));
                dims.Add(Line(ex - tick, oy, ex + tick, oy));
                dims.Add(Line(ex, oy, ex, yBottom));
                dims.Add(Tick(ex, oy, tick));
                dims.Add(Tick(ex, yBottom, tick));
                dims.Add(Label(Num.Format(s.Elevation) + " mm", ex - textHeight * 0.9, (oy + yBottom) / 2, 90));
            }

            Add(dims.Group(), "O'lchamlar");
        }

        private static string TitleText(SignModel model)
        {
            MockupSettings s = model.Settings;
            return string.Format("{0} — {1} × {2} × {3} mm — M 1:{4}",
                model.Design.Name, Num.Format(s.Width), Num.Format(s.Height), Num.Format(s.Depth), s.Scale);
        }

        private void DrawTitle(SignModel model, Layout layout)
        {
            corel.Shape title = layer.CreateArtisticText(ox + layout.MinX, oy + layout.TitleY, TitleText(model));
            StyleText(title, textHeight * 1.3);
            PlaceLeftBottom(title, ox + layout.MinX, oy + layout.TitleY);
            Add(title, "Sarlavha");
        }

        // ---------------------------------------------------------------- yordamchilar

        private corel.Shape PartShape(Part part)
        {
            if (part.Round)
            {
                return layer.CreateEllipse2(
                    ox + (part.X + part.W / 2) * k,
                    oy + (part.Y + part.H / 2) * k,
                    part.W / 2 * k,
                    part.H / 2 * k);
            }
            return RectShape(part.X, part.Y, part.W, part.H, 0);
        }

        /// <summary>Model koordinatasida (mm) to'rtburchak.</summary>
        private corel.Shape RectShape(double x, double y, double width, double height, double radius)
        {
            double r = radius * k;
            return layer.CreateRectangle2(ox + x * k, oy + y * k, width * k, height * k, r, r, r, r);
        }

        private corel.Shape Line(double x0, double y0, double x1, double y1)
        {
            corel.Shape line = layer.CreateLineSegment(x0, y0, x1, y1);
            line.Outline.Width = Num.Clamp(textHeight * 0.06, 0.1, 3);
            line.Outline.Color.RGBAssign(InkColor.R, InkColor.G, InkColor.B);
            return line;
        }

        /// <summary>O'lcham chizig'i uchidagi qiya chiziqcha.</summary>
        private corel.Shape Tick(double x, double y, double size)
        {
            corel.Shape tick = Line(x - size / 2, y - size / 2, x + size / 2, y + size / 2);
            tick.Outline.Width = Num.Clamp(textHeight * 0.12, 0.2, 5);
            return tick;
        }

        private corel.Shape Label(string text, double centerX, double centerY, double angle)
        {
            corel.Shape label = layer.CreateArtisticText(centerX, centerY, text);
            StyleText(label, textHeight);
            if (angle != 0)
                label.Rotate(angle);
            label.CenterX = centerX;
            label.CenterY = centerY;
            return label;
        }

        /// <summary>Shrift o'lchami o'zgargandan keyin matnni chap-past burchagi bo'yicha joylaydi.</summary>
        private static void PlaceLeftBottom(corel.Shape shape, double left, double bottom)
        {
            shape.CenterX = left + shape.SizeWidth / 2;
            shape.CenterY = bottom + shape.SizeHeight / 2;
        }

        private void StyleText(corel.Shape text, double heightMm)
        {
            text.Text.Story.Font = "Arial";
            text.Text.Story.Size = (float)(heightMm * PointsPerMm);
            Fill(text, InkColor);
        }

        private void Fill(corel.Shape shape, WpfColor color)
        {
            shape.Fill.ApplyUniformFill(Rgb(color));
            shape.Outline.SetNoOutline();
        }

        private corel.Color Rgb(WpfColor color) => app.CreateRGBColor(color.R, color.G, color.B);

        private static WpfColor Shade(WpfColor color, double factor)
        {
            byte Channel(byte value) => (byte)Num.Clamp(value * factor, 0, 255);
            return WpfColor.FromRgb(Channel(color.R), Channel(color.G), Channel(color.B));
        }

        private void Add(corel.Shape shape, string name)
        {
            shape.Name = name;
            created.Add(shape);
        }

        private void DeleteCreated()
        {
            try
            {
                if (created != null && created.Count > 0)
                    created.Delete();
            }
            catch
            {
                // Qolgan obyektlarni Ctrl+Z bilan olib tashlash mumkin
            }
        }

        private void ZoomTo(corel.Shape shape)
        {
            try
            {
                shape.CreateSelection();
                app.ActiveWindow.ActiveView.ToFitSelection();
            }
            catch
            {
                // Zoom qilib bo'lmasa ham maket tayyor
            }
        }
    }
}
