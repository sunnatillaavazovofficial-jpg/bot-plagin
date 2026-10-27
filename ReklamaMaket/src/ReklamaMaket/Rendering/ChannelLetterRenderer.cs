using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;
using corel = Corel.Interop.VGCore;
using WpfColor = System.Windows.Media.Color;

namespace ReklamaMaket.Rendering
{
    /// <summary>Chizilgan dizaynning asosiy qismlari (ChatGPT'ga yuborish uchun).</summary>
    public sealed class DrawResult
    {
        /// <summary>Kunduzgi ko'rinish (fon + harflar), o'lcham chiziqlarisiz.</summary>
        public corel.Shape DayView { get; set; }

        /// <summary>Kechki ko'rinish (chizilmagan bo'lsa null).</summary>
        public corel.Shape NightView { get; set; }
    }

    /// <summary>
    /// Hajmli harf (channel letters) dizaynini Corelga vektor obyektlar sifatida chizadi:
    /// kunduzgi va kechki ko'rinish, yon kesim sxemasi, o'lcham chiziqlari va sarlavha.
    /// Hammasi bitta "Undo" (Ctrl+Z) bilan bekor qilinadi.
    /// </summary>
    public sealed class ChannelLetterRenderer
    {
        private const double PointsPerMm = 72 / 25.4;
        private const double MaxPageSize = 45000;
        private const int MaxDots = 1500;

        private static readonly WpfColor Ink = Rgb(0x222222);
        private static readonly WpfColor WarningInk = Rgb(0xC62828);
        private static readonly WpfColor LeaderInk = Rgb(0x8A8F96);
        private static readonly WpfColor RayColor = Rgb(0xE0A100);
        private static readonly WpfColor LedColor = Rgb(0xFFD54A);
        private static readonly WpfColor WallSection = Rgb(0xC9C2B8);
        private static readonly WpfColor Steel = Rgb(0xA3A9AF);
        private static readonly WpfColor ClearAcrylic = Rgb(0xDDE8EE);
        private static readonly WpfColor Silver = Rgb(0xC9CED3);

        private static readonly WpfColor WallBehind = ColorMath.WallBehind;
        private static readonly WpfColor Diffuser = ColorMath.Diffuser;

        private readonly corel.Application app;
        private readonly List<corel.Shape> everything = new List<corel.Shape>();
        private readonly List<corel.Shape> temporaries = new List<corel.Shape>();
        private corel.ShapeRange finals;
        private corel.Document doc;
        private corel.Layer layer;

        private SignSettings s;
        private RuleResult rules;
        private SignLayout lay;
        private double k;      // haqiqiy mm -> sahifa mm
        private double th;     // sahifadagi yozuv balandligi, mm
        private double ox, oy; // kunduzgi ko'rinishning chap-past burchagi (sahifada)
        private double viewW, viewH;

        // Kunduzgi ko'rinish joyida turgan andozalar (oxirida o'chiriladi)
        private corel.Shape logoTemplate;
        private corel.Shape silhouette;
        private corel.Shape dotsTemplate;
        private corel.Shape dotGlowTemplate;
        private corel.Shape patternTemplate;

        public ChannelLetterRenderer(corel.Application app)
        {
            this.app = app;
        }

        private struct Frame
        {
            public double NightX;
            public double SectionX;
            public double SectionHeight;
            public double MinX;
            public double MinY;
            public double MaxX;
            public double MaxY;
            public double CaptionY;
            public double TitleY;
        }

        public DrawResult Draw(corel.Document doc, SignSettings settings, LogoInfo logo)
        {
            if (logo == null)
                throw new UserException("Avval logoni tanlang.");

            this.doc = doc;
            s = settings;
            rules = SignRules.Check(settings);
            lay = LogoLayout.Compute(settings);
            k = 1.0 / Math.Max(1, s.Scale);
            viewW = lay.BackgroundWidth * k;
            viewH = lay.BackgroundHeight * k;
            th = Num.Clamp(Math.Sqrt(viewW * viewH) * 0.035, 2.5, 60);

            var result = new DrawResult();
            corel.cdrUnit oldUnit = doc.Unit;
            doc.BeginCommandGroup("Reklama: hajmli harf");
            try
            {
                app.Optimization = true;
                app.EventsEnabled = false;
                doc.Unit = corel.cdrUnit.cdrMillimeter;
                finals = app.CreateShapeRange();

                Frame frame = ComputeFrame();
                Place(doc, logo, frame);
                PrepareTemplates(logo);

                result.DayView = Final(DrawView(false, ox, oy), "Kunduzi");
                if (s.DrawNight)
                    result.NightView = Final(DrawView(true, ox + frame.NightX, oy), "Kechasi");
                if (s.DrawSection)
                    Final(DrawSection(ox + frame.SectionX, oy, frame.SectionHeight), "Yon kesim");
                if (s.DrawDimensions)
                    Final(DrawDimensions(), "O'lchamlar");
                Final(DrawCaptions(frame), "Sarlavha");

                DeleteAll(temporaries);
            }
            catch
            {
                DeleteAll(temporaries);
                DeleteAll(everything);
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

            try
            {
                finals.CreateSelection();
                app.ActiveWindow.ActiveView.ToFitSelection();
            }
            catch
            {
                // Zoom qilib bo'lmasa ham dizayn tayyor
            }
            return result;
        }

        // ================================================================ joylashuv

        private bool HasBackgroundDims => s.Background.Kind != BackgroundKind.Wall;

        private Frame ComputeFrame()
        {
            double gap = Math.Max(viewW, viewH) * 0.1 + th * 6;
            double dimSpace = s.DrawDimensions ? th * (HasBackgroundDims ? 7 : 4.5) : 0;

            var frame = new Frame
            {
                MinX = -dimSpace,
                MinY = -Math.Max(dimSpace, s.DrawSection ? th * 2.5 : 0),
                SectionHeight = Math.Max(viewH, th * 15)
            };

            double right = viewW;
            if (s.DrawNight)
            {
                frame.NightX = viewW + gap;
                right = frame.NightX + viewW;
            }
            if (s.DrawSection)
            {
                frame.SectionX = right + gap;
                int longest = SectionLabels().Max(label => label.Length);
                right = frame.SectionX + frame.SectionHeight * 0.72 + longest * th * 0.8 * 0.58 + th;
            }
            frame.MaxX = right;

            double top = Math.Max(viewH, s.DrawSection ? frame.SectionHeight : 0);
            frame.CaptionY = top + th * 1.0;
            frame.TitleY = frame.CaptionY + th * 3.2;
            int titleLines = 2 + rules.Warnings.Count;
            frame.MaxY = frame.TitleY + titleLines * th * 1.9;

            // Uzun sarlavha sahifadan chiqmasin
            double titleWidth = TitleLines().Max(line => line.Length) * th * 1.1 * 0.58;
            frame.MaxX = Math.Max(frame.MaxX, frame.MinX + titleWidth);
            return frame;
        }

        private void Place(corel.Document doc, LogoInfo logo, Frame frame)
        {
            double width = frame.MaxX - frame.MinX;
            double height = frame.MaxY - frame.MinY;
            double pad = Math.Max(10, Math.Max(width, height) * 0.04);

            if (s.NewPage)
            {
                corel.Page page = doc.AddPagesEx(1,
                    Num.Clamp(width + 2 * pad, 10, MaxPageSize),
                    Num.Clamp(height + 2 * pad, 10, MaxPageSize));
                page.Activate();
                CorelService.TryRename(page, s.Letter.Name);
                layer = page.ActiveLayer;
                ox = page.CenterX - page.SizeWidth / 2 + pad - frame.MinX;
                oy = page.CenterY - page.SizeHeight / 2 + pad - frame.MinY;
                return;
            }

            layer = doc.ActiveLayer;
            try
            {
                // Logoning o'ng tomoniga
                ox = logo.Shapes.RightX + pad - frame.MinX;
                oy = logo.Shapes.BottomY - frame.MinY;
            }
            catch
            {
                corel.Page page = doc.ActivePage;
                ox = page.CenterX - page.SizeWidth / 2 + pad - frame.MinX;
                oy = page.CenterY - page.SizeHeight / 2 + pad - frame.MinY;
            }
        }

        /// <summary>Logo nusxasi va undan yasalgan bitta yaxlit kontur (silhouette) — kunduzgi ko'rinish joyida.</summary>
        private void PrepareTemplates(LogoInfo logo)
        {
            corel.ShapeRange copies;
            try
            {
                copies = logo.Shapes.Duplicate(0, 0);
            }
            catch (Exception ex)
            {
                throw new UserException("Logo topilmadi (o'chirilgan bo'lishi mumkin). Logoni qayta tanlab, \"Tanlangan logoni olish\" tugmasini bosing.", ex);
            }

            logoTemplate = Temp(copies.Count > 1 ? copies.Group() : copies.FirstShape);
            logoTemplate.MoveToLayer(layer);
            logoTemplate.SetSize(lay.Logo.Width * k, lay.Logo.Height * k);
            logoTemplate.CenterX = ox + (lay.Logo.X + lay.Logo.Width / 2) * k;
            logoTemplate.CenterY = oy + (lay.BackgroundHeight - lay.Logo.Y - lay.Logo.Height / 2) * k;

            try
            {
                corel.Shape copy = Temp(logoTemplate.Duplicate(0, 0));
                corel.ShapeRange parts = copy.UngroupAllEx();
                if (parts == null || parts.Count == 0)
                {
                    // Guruh emas — bitta obyektning o'zi
                    parts = app.CreateShapeRange();
                    parts.Add(copy);
                }
                foreach (corel.Shape part in parts)
                {
                    Temp(part);
                    try
                    {
                        part.ConvertToCurves();
                    }
                    catch
                    {
                        // Allaqachon egri chiziq
                    }
                }
                silhouette = Temp(parts.Count > 1 ? parts.Combine() : parts.FirstShape);
            }
            catch (Exception ex)
            {
                throw new UserException("Logoni yaxlit konturga aylantirib bo'lmadi. Logo vektor (egri chiziq yoki matn) bo'lishi kerak — " +
                                        "rasm (bitmap) bo'lsa, avval Trace Bitmap qiling.", ex);
            }
            Fill(silhouette, Ink);
            silhouette.Outline.SetNoOutline();

            if (!rules.CanBe3D)
                return;

            LetterStyle letter = s.Letter;
            if (letter.Has(LightZones.Dots))
            {
                double spacing, diameter, clearance;
                if (letter.Face == FaceMaterial.Perforated)
                {
                    spacing = Num.Clamp(s.LogoHeight * 0.03, 8, 30);
                    diameter = spacing * 0.5;
                    clearance = diameter / 2 + letter.TrimMm * 1.5;
                }
                else if (letter.Face == FaceMaterial.Metal)
                {
                    spacing = Num.Clamp(s.LogoHeight * 0.12, 40, 200);
                    diameter = spacing * 0.55;
                    clearance = diameter / 2 + letter.TrimMm;
                }
                else
                {
                    spacing = Num.Clamp(s.LogoHeight * 0.07, 20, 100);
                    diameter = spacing * 0.35;
                    clearance = diameter / 2 + letter.TrimMm;
                }

                List<Point> points = InsidePoints(spacing * k, clearance * k, letter.Face == FaceMaterial.Perforated, out double scale);
                dotsTemplate = Temp(Circles(points, diameter * k * scale));
                if (letter.Face != FaceMaterial.Perforated)
                    dotGlowTemplate = Temp(Circles(points, diameter * k * scale * 1.9));
            }

            if (letter.Has(LightZones.Pattern))
            {
                double spacing = Num.Clamp(s.LogoHeight * 0.08, 25, 120);
                double side = spacing * 0.5;
                List<Point> points = InsidePoints(spacing * k, side * 0.75 * k, true, out double scale);
                patternTemplate = Temp(Diamonds(points, side * k * scale));
            }
        }

        // ================================================================ kunduzi / kechasi

        private List<corel.Shape> DrawView(bool night, double vx, double vy)
        {
            var items = new List<corel.Shape>();
            DrawBackground(items, night, vx, vy);
            DrawLetters(items, night, vx - ox, vy - oy);
            return items;
        }

        private void DrawBackground(List<corel.Shape> items, bool night, double vx, double vy)
        {
            WpfColor color = s.BackgroundColor;
            bool lit = night && rules.BackgroundLit;
            WpfColor surface = night ? (lit ? Lit(color) : Shade(color, 0.22)) : color;
            double minSide = Math.Min(lay.BackgroundWidth, lay.BackgroundHeight);

            switch (s.Background.Kind)
            {
                case BackgroundKind.Wall:
                    items.Add(Box(vx, vy, viewW, viewH, surface));
                    break;

                case BackgroundKind.Panel:
                    items.Add(Outlined(Box(vx, vy, viewW, viewH, surface), Shade(surface, 0.75)));
                    break;

                case BackgroundKind.Cassette:
                {
                    items.Add(Outlined(Box(vx, vy, viewW, viewH, surface), Shade(surface, 0.6)));
                    double fold = Num.Clamp(minSide * 0.02, 10, 30) * k;
                    corel.Shape line = Box(vx + fold, vy + fold, viewW - 2 * fold, viewH - 2 * fold, surface);
                    line.Fill.ApplyNoFill();
                    items.Add(Outlined(line, Shade(surface, 0.7)));
                    break;
                }

                case BackgroundKind.Lightbox:
                {
                    double profile = Num.Clamp(minSide * 0.03, 25, 60) * k;
                    corel.Shape frame = Box(vx, vy, viewW, viewH, Steel);
                    if (night)
                        Fill(frame, Shade(Steel, 0.3));
                    else
                        Metallic(frame, Steel);
                    items.Add(frame);
                    items.Add(Box(vx + profile, vy + profile, viewW - 2 * profile, viewH - 2 * profile, surface));
                    break;
                }

                case BackgroundKind.AcrylicPlate:
                {
                    corel.Shape plate = Outlined(Box(vx, vy, viewW, viewH, surface), Shade(surface, 0.7));
                    if (!night)
                        plate.Transparency.ApplyUniformTransparency(20);
                    items.Add(plate);

                    double d = Num.Clamp(minSide * 0.03, 20, 40) * k;
                    WpfColor bolt = night ? Shade(Steel, 0.3) : Steel;
                    foreach (double cx in new[] { vx + d * 1.6, vx + viewW - d * 1.6 })
                    {
                        foreach (double cy in new[] { vy + d * 1.6, vy + viewH - d * 1.6 })
                            items.Add(Outlined(Circle(cx, cy, d), Shade(bolt, 0.7)));
                    }
                    break;
                }

                case BackgroundKind.PatternSheet:
                case BackgroundKind.FramedMesh:
                case BackgroundKind.WoodFrameMesh:
                    DrawPatternBackground(items, night, vx, vy);
                    break;

                case BackgroundKind.HorizontalSlats:
                case BackgroundKind.VerticalSlats:
                    DrawSlats(items, night, vx, vy);
                    break;

                case BackgroundKind.AcpBox:
                    // Quti yuzi yonmaydi — faqat o'yilgan logo yonadi
                    items.Add(Outlined(Box(vx, vy, viewW, viewH, night ? Shade(color, 0.22) : color), Shade(color, 0.55)));
                    break;
            }

            if (s.EngravedBorder && s.Background.CanHaveBorder)
            {
                double inset = Num.Clamp(minSide * 0.04, 25, 80) * k;
                double width = Num.Clamp(minSide * 0.008, 4, 12) * k;
                corel.Shape border = Box(vx + inset, vy + inset, viewW - 2 * inset, viewH - 2 * inset, Silver);
                border.Fill.ApplyNoFill();
                Stroke(border, width, night ? Shade(Silver, 0.3) : Silver);
                items.Add(border);
            }
        }

        private Rect BackgroundRect => new Rect(0, 0, lay.BackgroundWidth, lay.BackgroundHeight);

        /// <summary>Gulli list / setka: ramka, list va teshiklar (kechasi — yorug'lik darajasi bo'yicha).</summary>
        private void DrawPatternBackground(List<corel.Shape> items, bool night, double vx, double vy)
        {
            BackgroundStyle background = s.Background;
            bool lit = night && rules.BackgroundLit;
            bool lightBox = s.BackgroundLight != BackgroundLight.None;
            double frameWidth = background.FrameWidth;
            Rect inner = new Rect(frameWidth, frameWidth,
                Math.Max(lay.BackgroundWidth - 2 * frameWidth, 1), Math.Max(lay.BackgroundHeight - 2 * frameWidth, 1));

            // Ramka
            if (frameWidth > 0)
            {
                corel.Shape frame = LocalBox(vx, vy, BackgroundRect, background.FrameColor);
                if (night)
                    Fill(frame, Shade(background.FrameColor, 0.3));
                else
                    Metallic(frame, background.FrameColor);
                items.Add(frame);
            }

            // List (setka simlari)
            WpfColor sheet = night ? Shade(s.BackgroundColor, lit ? 0.45 : 0.22) : s.BackgroundColor;
            items.Add(LocalBox(vx, vy, inner, sheet));

            // Teshiklar: orqada yorug'lik qutisi (diffuzor) yoki devor ko'rinadi
            Rect holeArea = inner;
            if (background.Kind == BackgroundKind.PatternSheet)
            {
                double edge = Num.Clamp(Math.Min(inner.Width, inner.Height) * 0.03, 15, 40);
                holeArea = new Rect(inner.X + edge, inner.Y + edge, Math.Max(inner.Width - 2 * edge, 1), Math.Max(inner.Height - 2 * edge, 1));
            }
            List<Hole> holes = PatternGeometry.Holes(s.Pattern, holeArea, out _);
            WpfColor behind = lightBox ? Diffuser : Shade(WallBehind, 0.75);
            if (!lit)
            {
                items.Add(Polygons(holes.Select(h => h.Points), vx, vy, night ? Shade(behind, 0.15) : behind));
            }
            else
            {
                for (int level = 0; level < PatternGeometry.LightLevels; level++)
                {
                    int current = level;
                    var group = holes.Where(h => PatternGeometry.Level(s.BackgroundLight, h.Center, BackgroundRect, lay.Logo) == current)
                                     .Select(h => h.Points);
                    items.Add(Polygons(group, vx, vy, LightLevelColor(behind, current)));
                }
            }

            // Ramkadagi setka: oraliq ustunlar (rasmdagidek)
            if (background.Kind == BackgroundKind.FramedMesh && inner.Width > 1500)
            {
                int bars = (int)Math.Round(inner.Width / 1000) - 1;
                for (int i = 1; i <= bars; i++)
                {
                    double x = inner.X + inner.Width * i / (bars + 1);
                    corel.Shape bar = LocalBox(vx, vy, new Rect(x - frameWidth * 0.4, inner.Y, frameWidth * 0.8, inner.Height), background.FrameColor);
                    if (night)
                        Fill(bar, Shade(background.FrameColor, 0.3));
                    items.Add(bar);
                }
            }
        }

        /// <summary>Gorizontal reykalar (kronshteynda) yoki vertikal reykali panel.</summary>
        private void DrawSlats(List<corel.Shape> items, bool night, double vx, double vy)
        {
            bool horizontal = s.Background.Kind == BackgroundKind.HorizontalSlats;
            bool lit = night && rules.BackgroundLit;
            WpfColor slatColor = night ? Shade(s.BackgroundColor, 0.25) : s.BackgroundColor;
            List<Rect> slats = PatternGeometry.Slats(BackgroundRect, horizontal, out _);

            if (horizontal)
            {
                // Oraliqlardan devor va vertikal kronshteynlar ko'rinadi
                items.Add(LocalBox(vx, vy, BackgroundRect, night ? Shade(WallBehind, 0.18) : WallBehind));
                int posts = Math.Max(2, (int)Math.Round(lay.BackgroundWidth / 1200) + 1);
                for (int i = 0; i < posts; i++)
                {
                    double x = lay.BackgroundWidth * (i + 0.5) / posts;
                    items.Add(LocalBox(vx, vy, new Rect(x - 20, 0, 40, lay.BackgroundHeight), Shade(slatColor, 0.75)));
                }
            }
            else
            {
                // Qovurg'alar orasidagi chuqurchalar
                items.Add(LocalBox(vx, vy, BackgroundRect, night ? Shade(s.BackgroundColor, 0.12) : Shade(s.BackgroundColor, 0.55)));
            }

            if (lit)
            {
                List<Rect> gaps = PatternGeometry.SlatGaps(slats, horizontal, 16);
                for (int level = 1; level < PatternGeometry.LightLevels; level++)
                {
                    int current = level;
                    var group = gaps.Where(g => PatternGeometry.Level(s.BackgroundLight, new Point(g.X + g.Width / 2, g.Y + g.Height / 2), BackgroundRect, lay.Logo) == current)
                                    .Select(RectPoints);
                    items.Add(Polygons(group, vx, vy, LightLevelColor(WallBehind, current)));
                }
            }

            foreach (Rect slat in slats)
            {
                corel.Shape shape = LocalBox(vx, vy, slat, slatColor);
                if (!night && horizontal)
                    Metallic(shape, slatColor);
                items.Add(shape);
            }
        }

        private static Point[] RectPoints(Rect r) => new[]
        {
            new Point(r.Left, r.Top), new Point(r.Right, r.Top), new Point(r.Right, r.Bottom), new Point(r.Left, r.Bottom)
        };

        /// <summary>Kechasi yorug'lik darajasiga mos rang (0 — qorong'i, eng yuqori — nur rangi).</summary>
        private WpfColor LightLevelColor(WpfColor behind, int level) =>
            ColorMath.LightLevel(behind, s.LightColor, level, PatternGeometry.LightLevels);

        /// <summary>Fon ichidagi to'rtburchak (mm, chap-yuqoridan, Y pastga) — sahifaga.</summary>
        private corel.Shape LocalBox(double vx, double vy, Rect r, WpfColor color)
        {
            return Box(vx + r.X * k, vy + (lay.BackgroundHeight - r.Y - r.Height) * k, r.Width * k, r.Height * k, color);
        }

        /// <summary>Ko'p ko'pburchakni bitta egri chiziq obyekti qilib chizadi (minglab teshik uchun tez).</summary>
        private corel.Shape Polygons(IEnumerable<Point[]> polygons, double vx, double vy, WpfColor color)
        {
            corel.Curve curve = app.CreateCurve(doc);
            int count = 0;
            foreach (Point[] polygon in polygons)
            {
                corel.SubPath path = curve.CreateSubPath(vx + polygon[0].X * k, vy + (lay.BackgroundHeight - polygon[0].Y) * k);
                for (int i = 1; i < polygon.Length; i++)
                    path.AppendLineSegment(vx + polygon[i].X * k, vy + (lay.BackgroundHeight - polygon[i].Y) * k);
                path.Closed = true;
                count++;
            }
            if (count == 0)
                return null;

            corel.Shape shape = Track(layer.CreateCurve(curve));
            Fill(shape, color);
            shape.Outline.SetNoOutline();
            return shape;
        }

        private void DrawLetters(List<corel.Shape> items, bool night, double dx, double dy)
        {
            if (s.Letter.IsCut)
            {
                DrawCutLogo(items, night, dx, dy);
                return;
            }

            LetterStyle letter = s.Letter;
            bool volumetric = rules.CanBe3D;
            bool lit = night && rules.LettersLit;
            WpfColor light = s.LightColor;
            double glow = Num.Clamp(s.LogoHeight * 0.06, 15, 150) * k;

            // 0. Kunduzi — harfning fonga tushgan soyasi (chuqurlik hissi, oq fonda oq harf ham ko'rinadi)
            if (!night && volumetric)
            {
                double offset = ShadowOffset() * k;
                corel.Shape shadow = Dup(silhouette, dx + offset, dy - offset);
                Fill(shadow, Rgb(0x000000));
                shadow.Outline.SetNoOutline();
                shadow.Transparency.ApplyUniformTransparency(78);
                items.Add(shadow);
            }

            // 1. Nur (eng orqada)
            if (lit)
            {
                if (letter.Has(LightZones.Halo))
                    Glow(items, dx, dy, light, glow, 4);
                if (letter.Has(LightZones.SideFull) || letter.Has(LightZones.Rim))
                    Glow(items, dx, dy, light, glow * 0.55, 3);
                else if (letter.Has(LightZones.SidePartial))
                    Glow(items, dx, dy, light, glow * 0.3, 2);
                if (letter.Has(LightZones.Face))
                    Glow(items, dx, dy, Lit(s.FaceColor), glow * 0.4, 3);
            }

            // 2. Chet: bort qirrasi yoki yonadigan kontur
            double trim = volumetric ? letter.TrimMm * k : 0;
            bool rimLit = lit && (letter.Has(LightZones.SideFull) || letter.Has(LightZones.SidePartial) || letter.Has(LightZones.Rim));
            if (rimLit || trim > 0)
            {
                WpfColor rimColor = rimLit ? light : (night ? Shade(s.ReturnColor, 0.25) : s.ReturnColor);
                double width = rimLit ? Math.Max(trim, 3 * k) : trim;
                corel.Shape rim = Dup(silhouette, dx, dy);
                Fill(rim, rimColor);
                Stroke(rim, width * 2, rimColor);
                items.Add(rim);
            }

            // 3. Yuz
            bool faceLit = lit && letter.Has(LightZones.Face);
            corel.Shape face;
            if (s.KeepLogoColors && (!night || faceLit))
            {
                face = Dup(logoTemplate, dx, dy);
            }
            else
            {
                face = Dup(silhouette, dx, dy);
                WpfColor color = night ? (faceLit ? Lit(s.FaceColor) : Shade(s.FaceColor, 0.22)) : s.FaceColor;
                if (!night && letter.Face == FaceMaterial.Metal)
                    Metallic(face, color);
                else
                    Fill(face, color);
                face.Outline.SetNoOutline();
                if (!night && letter.Face == FaceMaterial.ClearAcrylic && volumetric)
                    face.Transparency.ApplyUniformTransparency(12);
            }
            items.Add(face);

            if (!night && letter.Bevel && volumetric)
            {
                corel.Shape bevel = Dup(silhouette, dx, dy);
                bevel.Fill.ApplyNoFill();
                Stroke(bevel, Math.Max(4 * k, th * 0.04), Shade(s.FaceColor, 0.8));
                items.Add(bevel);
            }

            // 4. Nuqtalar (LED, lampochka, perforatsiya) va naqsh
            if (dotsTemplate != null)
            {
                WpfColor dayColor = letter.Face == FaceMaterial.Perforated ? Shade(s.FaceColor, 2.4)
                    : letter.Face == FaceMaterial.Metal ? Rgb(0xF3EBD8) : Rgb(0xD9D9D9);
                bool dotsLit = lit && letter.Has(LightZones.Dots);
                if (dotsLit && dotGlowTemplate != null)
                {
                    corel.Shape halo = Dup(dotGlowTemplate, dx, dy);
                    Fill(halo, light);
                    halo.Transparency.ApplyUniformTransparency(65);
                    items.Add(halo);
                }
                corel.Shape dots = Dup(dotsTemplate, dx, dy);
                Fill(dots, dotsLit ? light : (night ? Shade(dayColor, 0.25) : dayColor));
                items.Add(dots);
            }

            if (patternTemplate != null)
            {
                corel.Shape pattern = Dup(patternTemplate, dx, dy);
                WpfColor dayColor = Rgb(0xE8C766);
                Fill(pattern, lit ? light : (night ? Shade(dayColor, 0.25) : dayColor));
                items.Add(pattern);
            }
        }

        /// <summary>Fonga o'yilgan logo: o'yiq + akril / chiqib turuvchi harf / orqa panel / gravirovka.</summary>
        private void DrawCutLogo(List<corel.Shape> items, bool night, double dx, double dy)
        {
            LetterStyle letter = s.Letter;
            bool lit = night && rules.LettersLit;
            WpfColor light = s.LightColor;
            double glow = Num.Clamp(s.LogoHeight * 0.06, 15, 150) * k;
            // 20 sm dan past logo chiqib turolmaydi — yassi o'yiq bo'ladi
            CutMode mode = letter.Cut == CutMode.PushThrough && !rules.CanBe3D ? CutMode.RoutedLit : letter.Cut;
            WpfColor edge = Shade(s.BackgroundColor, 0.45);

            if (mode == CutMode.PushThrough)
            {
                if (!night)
                {
                    double offset = Num.Clamp(letter.Protrusion(s.Depth) * 0.35, 3, 20) * k;
                    corel.Shape shadow = Dup(silhouette, dx + offset, dy - offset);
                    Fill(shadow, Rgb(0x000000));
                    shadow.Outline.SetNoOutline();
                    shadow.Transparency.ApplyUniformTransparency(78);
                    items.Add(shadow);
                }
                if (lit)
                    Glow(items, dx, dy, light, glow * 0.5, 3);

                WpfColor rimColor = lit ? light : (night ? Shade(s.ReturnColor, 0.25) : s.ReturnColor);
                corel.Shape rim = Dup(silhouette, dx, dy);
                Fill(rim, rimColor);
                Stroke(rim, Math.Max(letter.TrimMm, 3) * k * 2, rimColor);
                items.Add(rim);
            }
            else if (lit && mode == CutMode.RoutedLit)
            {
                Glow(items, dx, dy, Lit(s.FaceColor), glow * 0.35, 3);
            }

            if (mode != CutMode.PushThrough)
            {
                // O'yiq cheti (frezer izi)
                corel.Shape cutEdge = Dup(silhouette, dx, dy);
                Fill(cutEdge, edge);
                Stroke(cutEdge, Math.Max(2.5 * k, th * 0.03), night ? Shade(edge, 0.4) : edge);
                items.Add(cutEdge);
            }

            bool faceLit = lit && (mode == CutMode.RoutedLit || mode == CutMode.PushThrough);
            corel.Shape face;
            if (s.KeepLogoColors && mode == CutMode.RoutedLit && (!night || faceLit))
            {
                face = Dup(logoTemplate, dx, dy);
            }
            else
            {
                face = Dup(silhouette, dx, dy);
                WpfColor color = night ? (faceLit ? Lit(s.FaceColor) : Shade(s.FaceColor, 0.22)) : s.FaceColor;
                if (!night && mode == CutMode.Engraved)
                    Metallic(face, color);
                else
                    Fill(face, color);
                face.Outline.SetNoOutline();
            }
            items.Add(face);
        }

        /// <summary>Soya siljishi (mm): qalinlik va distansiyaga bog'liq.</summary>
        private double ShadowOffset() => Num.Clamp((s.Depth + (s.Letter.OnStandoffs ? 30 : 0)) * 0.3, 4, 60);

        /// <summary>Konturdan tarqaladigan yumshoq nur: bir necha shaffof qatlam.</summary>
        private void Glow(List<corel.Shape> items, double dx, double dy, WpfColor color, double width, int layers)
        {
            for (int i = 0; i < layers; i++)
            {
                double w = width * (layers - i) / layers;
                corel.Shape shape = Dup(silhouette, dx, dy);
                Fill(shape, color);
                Stroke(shape, w * 2, color);
                int transparency = layers == 1 ? 60 : 88 - i * 30 / (layers - 1);
                shape.Transparency.ApplyUniformTransparency(transparency);
                items.Add(shape);
            }
        }

        // ================================================================ yon kesim (sxema)

        private string BackgroundLabel()
        {
            switch (s.Background.Kind)
            {
                case BackgroundKind.Panel:
                    return "Fon: kompozit panel " + Num.Format(s.BackgroundDepth) + " mm";
                case BackgroundKind.Cassette:
                    return "Fon: kasseta " + Num.Format(s.BackgroundDepth) + " mm";
                case BackgroundKind.Lightbox:
                    return "Fon: laytboks " + Num.Format(s.BackgroundDepth) + " mm" + (rules.BackgroundLit ? " (yonadi)" : "");
                case BackgroundKind.AcrylicPlate:
                    return "Fon: akril plita " + Num.Format(s.BackgroundDepth) + " mm";
                case BackgroundKind.PatternSheet:
                    return "Fon: gulli temir list" + (rules.BackgroundLit ? ", orqasida LED" : string.Empty);
                case BackgroundKind.FramedMesh:
                    return "Fon: ramkadagi setka" + (rules.BackgroundLit ? ", orqasida LED" : string.Empty);
                case BackgroundKind.WoodFrameMesh:
                    return "Fon: yog'och ramka + setka" + (rules.BackgroundLit ? ", LED" : string.Empty);
                case BackgroundKind.HorizontalSlats:
                    return "Fon: gorizontal reykalar, kronshteyn";
                case BackgroundKind.VerticalSlats:
                    return "Fon: vertikal reykali panel";
                case BackgroundKind.AcpBox:
                    return "Quti: alyukobond, " + Num.Format(s.BackgroundDepth) + " mm";
                default:
                    return null;
            }
        }

        private bool OnSpacers => s.Letter.OnStandoffs && rules.CanBe3D;

        private List<string> SectionLabels()
        {
            if (s.Letter.IsCut)
                return CutSectionLabels();

            var labels = new List<string> { "Devor" };
            string background = BackgroundLabel();
            if (background != null)
                labels.Add(background);
            if (OnSpacers)
                labels.Add("Distansiya ~30 mm");
            if (rules.CanBe3D)
            {
                labels.Add(s.Letter.Has(LightZones.Halo) ? "Orqa: shaffof akril" : "Orqa: PVX / alyumin");
                labels.Add(s.Letter.ReturnLabel);
                labels.Add(s.Letter.FaceLabel);
                if (rules.LettersLit)
                    labels.Add("LED modullar");
            }
            else
            {
                labels.Add("Yassi harf (akril / PVX)");
            }
            return labels;
        }

        private CutMode SectionCutMode =>
            s.Letter.Cut == CutMode.PushThrough && !rules.CanBe3D ? CutMode.RoutedLit : s.Letter.Cut;

        /// <summary>Fon qutisi bormi (o'yilgan logo uchun ichida LED joylashadi).</summary>
        private bool CutHasBox => s.Background.Kind == BackgroundKind.AcpBox || s.Background.Kind == BackgroundKind.Lightbox ||
                                  s.Background.Kind == BackgroundKind.Cassette;

        private List<string> CutSectionLabels()
        {
            var labels = new List<string> { "Devor" };
            if (CutHasBox)
                labels.Add("Quti: alyukobond, " + Num.Format(s.BackgroundDepth) + " mm");
            labels.Add(s.Letter.ReturnLabel);
            switch (SectionCutMode)
            {
                case CutMode.RoutedLit:
                    labels.Add("Akril " + Num.Format(s.Depth) + " mm, yuz bilan bir tekis");
                    break;
                case CutMode.PushThrough:
                    labels.Add("Akril harf " + Num.Format(s.Depth) + " mm, " + Num.Format(s.Letter.Protrusion(s.Depth)) + " mm chiqib turadi");
                    break;
                case CutMode.RoutedBacking:
                    labels.Add("Orqa panel (boshqa rang)");
                    break;
                case CutMode.Engraved:
                    labels.Add("Gravirovka: alyuminiy ko'rinadi");
                    break;
            }
            if (rules.LettersLit && CutHasBox)
                labels.Add("LED modullar");
            return labels;
        }

        /// <summary>O'yilgan logo kesimi: quti, yuz va o'yiq (akril / chiqib turuvchi harf / orqa panel / gravirovka).</summary>
        private List<corel.Shape> DrawCutSection(double sx, double sy, double height)
        {
            var items = new List<corel.Shape>();
            var anchors = new List<Point>();
            CutMode mode = SectionCutMode;
            bool lit = rules.LettersLit;
            double u = height / 10;
            double x = sx;

            // Devor
            items.Add(Outlined(Box(x, sy, u, height, WallSection), Ink));
            for (double hy = sy; hy + u <= sy + height + 0.001; hy += u * 0.8)
                items.Add(Line(x, hy, x + u, hy + u, LeaderInk, th * 0.03));
            anchors.Add(new Point(x + u / 2, sy + height - u / 2));
            x += u;

            double top = sy + 0.5 * u;
            double bottom = sy + height - 0.5 * u;
            double wall = 0.12 * u;
            double boxDepth = CutHasBox ? 2.2 * u : 0.2 * u;
            double faceX = x + boxDepth;
            double faceT = 0.15 * u;
            double mid = (top + bottom) / 2;
            double gapTop = mid + 1.6 * u;
            double gapBottom = mid - 1.6 * u;

            if (CutHasBox)
            {
                items.Add(Outlined(Box(x, top, wall, bottom - top, Shade(s.BackgroundColor, 0.85)), Ink));
                items.Add(Outlined(Box(x, bottom - wall, boxDepth, wall, s.BackgroundColor), Ink));
                items.Add(Outlined(Box(x, top, boxDepth, wall, s.BackgroundColor), Ink));
                anchors.Add(new Point(x + boxDepth / 2, bottom - wall / 2));
            }

            // Yuz (o'yiq joyida uzilgan)
            WpfColor faceColor = s.BackgroundColor;
            if (mode == CutMode.Engraved)
            {
                items.Add(Outlined(Box(faceX, top, faceT, bottom - top, faceColor), Ink));
                items.Add(Box(faceX + faceT * 0.5, gapBottom, faceT * 0.5, gapTop - gapBottom, Silver));
            }
            else
            {
                items.Add(Outlined(Box(faceX, gapTop, faceT, bottom - gapTop, faceColor), Ink));
                items.Add(Outlined(Box(faceX, top, faceT, gapBottom - top, faceColor), Ink));
            }
            anchors.Add(new Point(faceX + faceT / 2, gapTop + 0.6 * u));

            // O'yiqdagi element
            switch (mode)
            {
                case CutMode.RoutedLit:
                {
                    double acrylic = 0.35 * u;
                    items.Add(Outlined(Box(faceX + faceT - acrylic, gapBottom - 0.25 * u, acrylic, gapTop - gapBottom + 0.5 * u, s.FaceColor), Ink));
                    anchors.Add(new Point(faceX + faceT - acrylic / 2, mid));
                    break;
                }
                case CutMode.PushThrough:
                {
                    double back = 0.3 * u;
                    double protrusion = 0.6 * u;
                    items.Add(Outlined(Box(faceX - back, gapBottom + 0.05 * u, back + faceT + protrusion, gapTop - gapBottom - 0.1 * u, ClearAcrylic), Ink));
                    items.Add(Box(faceX + faceT + protrusion - 0.06 * u, gapBottom + 0.05 * u, 0.06 * u, gapTop - gapBottom - 0.1 * u, s.FaceColor));
                    anchors.Add(new Point(faceX + faceT + protrusion / 2, mid));
                    break;
                }
                case CutMode.RoutedBacking:
                {
                    double backingX = faceX - 0.45 * u;
                    items.Add(Outlined(Box(backingX, gapBottom - 0.8 * u, wall, gapTop - gapBottom + 1.6 * u, s.FaceColor), Ink));
                    anchors.Add(new Point(backingX + wall / 2, mid));
                    break;
                }
                case CutMode.Engraved:
                    anchors.Add(new Point(faceX + faceT * 0.75, mid));
                    break;
            }

            // LED va nur
            if (lit && CutHasBox)
            {
                double ledY = 0;
                for (int i = 1; i <= 3; i++)
                {
                    ledY = gapBottom + (gapTop - gapBottom) * i / 4;
                    items.Add(Outlined(Box(x + wall, ledY - 0.15 * u, 0.22 * u, 0.3 * u, LedColor), Ink));
                }
                anchors.Add(new Point(x + wall + 0.11 * u, ledY));

                double rayStart = faceX + faceT + (mode == CutMode.PushThrough ? 0.7 * u : 0.15 * u);
                foreach (double ry in new[] { mid - 1.0 * u, mid, mid + 1.0 * u })
                    items.Add(Line(rayStart, ry, rayStart + 1.0 * u, ry + (ry - mid) * 0.35, RayColor, th * 0.09));
                if (mode == CutMode.PushThrough)
                {
                    double rx = faceX + faceT + 0.3 * u;
                    items.Add(Line(rx, gapTop, rx + 0.2 * u, gapTop + 0.8 * u, RayColor, th * 0.09));
                    items.Add(Line(rx, gapBottom, rx + 0.2 * u, gapBottom - 0.8 * u, RayColor, th * 0.09));
                }
            }

            // Qalinlik o'lchami
            double label = th * 0.8;
            double dimY = sy + 0.1 * u;
            if (CutHasBox)
            {
                items.AddRange(DimLine(x, dimY, faceX + faceT, dimY, th * 0.3));
                items.Add(Label(Num.Format(s.BackgroundDepth) + " mm", (x + faceX + faceT) / 2, dimY - label * 0.9, label, 0));
            }

            // Yozuvlar
            List<string> labels = CutSectionLabels();
            double labelX = sx + height * 0.72;
            for (int i = 0; i < labels.Count && i < anchors.Count; i++)
            {
                double ly = sy + height - u * 0.4 - i * th * 1.6;
                items.Add(Line(anchors[i].X, anchors[i].Y, labelX - th * 0.3, ly, LeaderInk, th * 0.03));
                corel.Shape text = Label(labels[i], 0, 0, label, 0);
                text.CenterX = labelX + text.SizeWidth / 2;
                text.CenterY = ly;
                items.Add(text);
            }
            return items;
        }

        private List<corel.Shape> DrawSection(double sx, double sy, double height)
        {
            if (s.Letter.IsCut)
                return DrawCutSection(sx, sy, height);

            var items = new List<corel.Shape>();
            var anchors = new List<Point>();
            LetterStyle letter = s.Letter;
            bool volumetric = rules.CanBe3D;
            bool lit = rules.LettersLit;
            double u = height / 10;
            double x = sx;

            // Devor (shtrixlangan)
            items.Add(Outlined(Box(x, sy, u, height, WallSection), Ink));
            for (double hy = sy; hy + u <= sy + height + 0.001; hy += u * 0.8)
                items.Add(Line(x, hy, x + u, hy + u, LeaderInk, th * 0.03));
            anchors.Add(new Point(x + u / 2, sy + height - u / 2));
            x += u;

            // Fon
            switch (s.Background.Kind)
            {
                case BackgroundKind.Panel:
                    items.Add(Outlined(Box(x, sy + 0.5 * u, 0.18 * u, height - u, s.BackgroundColor), Ink));
                    anchors.Add(new Point(x + 0.09 * u, sy + height - 0.8 * u));
                    x += 0.18 * u;
                    break;

                case BackgroundKind.Cassette:
                case BackgroundKind.AcpBox:
                {
                    double boxDepth = s.Background.Kind == BackgroundKind.AcpBox ? 1.0 * u : 0.6 * u;
                    items.Add(Outlined(Box(x, sy + 0.4 * u, boxDepth, height - 0.8 * u, s.BackgroundColor), Ink));
                    anchors.Add(new Point(x + boxDepth / 2, sy + height - 0.7 * u));
                    x += boxDepth;
                    break;
                }

                case BackgroundKind.Lightbox:
                    items.Add(Outlined(Box(x, sy + 0.3 * u, 1.4 * u, height - 0.6 * u, Rgb(0xF4F4F4)), Ink));
                    items.Add(Outlined(Box(x + 1.28 * u, sy + 0.3 * u, 0.12 * u, height - 0.6 * u, s.BackgroundColor), Ink));
                    if (rules.BackgroundLit)
                    {
                        for (int i = 1; i <= 3; i++)
                            items.Add(Box(x + 0.15 * u, sy + height * i / 4 - 0.15 * u, 0.22 * u, 0.3 * u, LedColor));
                    }
                    anchors.Add(new Point(x + 0.7 * u, sy + height - 0.6 * u));
                    x += 1.4 * u;
                    break;

                case BackgroundKind.AcrylicPlate:
                    foreach (double by in new[] { sy + 1.2 * u, sy + height - 1.32 * u })
                        items.Add(Outlined(Box(x, by, 0.5 * u, 0.12 * u, Steel), Ink));
                    corel.Shape plate = Outlined(Box(x + 0.5 * u, sy + 0.5 * u, 0.3 * u, height - u, ClearAcrylic), Ink);
                    items.Add(plate);
                    anchors.Add(new Point(x + 0.65 * u, sy + height - 0.8 * u));
                    x += 0.8 * u;
                    break;

                case BackgroundKind.PatternSheet:
                case BackgroundKind.FramedMesh:
                case BackgroundKind.WoodFrameMesh:
                {
                    bool box = s.BackgroundLight != BackgroundLight.None;
                    double boxDepth = box ? 1.1 * u : 0.3 * u;
                    if (box)
                    {
                        items.Add(Outlined(Box(x, sy + 0.3 * u, boxDepth, height - 0.6 * u, Rgb(0xF4F4F4)), Ink));
                        if (rules.BackgroundLit)
                        {
                            for (int i = 1; i <= 3; i++)
                                items.Add(Box(x + 0.12 * u, sy + height * i / 4 - 0.15 * u, 0.22 * u, 0.3 * u, LedColor));
                        }
                    }
                    if (s.Background.FrameWidth > 0)
                    {
                        foreach (double fy in new[] { sy + 0.3 * u, sy + height - 0.65 * u })
                            items.Add(Outlined(Box(x + boxDepth - 0.35 * u, fy, 0.35 * u, 0.35 * u, s.Background.FrameColor), Ink));
                    }
                    double sheetX = x + boxDepth;
                    for (double py = sy + 0.4 * u; py < sy + height - 0.6 * u; py += 0.35 * u)
                        items.Add(Box(sheetX, py, 0.1 * u, 0.2 * u, Shade(s.BackgroundColor, 0.8)));
                    anchors.Add(new Point(sheetX + 0.05 * u, sy + height - 0.8 * u));
                    x = sheetX + 0.1 * u;
                    break;
                }

                case BackgroundKind.HorizontalSlats:
                {
                    foreach (double ay in new[] { sy + 0.8 * u, sy + height - 0.9 * u })
                        items.Add(Outlined(Box(x, ay, 0.3 * u, 0.1 * u, Steel), Ink));
                    items.Add(Outlined(Box(x + 0.3 * u, sy + 0.3 * u, 0.15 * u, height - 0.6 * u, Steel), Ink));
                    double slat = 0.45 * u;
                    for (double py = sy + 0.4 * u; py + slat < sy + height - 0.3 * u; py += slat * 1.7)
                        items.Add(Outlined(Box(x + 0.45 * u, py, slat, slat, s.BackgroundColor), Ink));
                    if (rules.BackgroundLit)
                    {
                        for (int i = 1; i <= 3; i++)
                            items.Add(Box(x + 0.32 * u, sy + height * i / 4, 0.12 * u, 0.25 * u, LedColor));
                    }
                    anchors.Add(new Point(x + 0.45 * u + slat / 2, sy + height - 0.8 * u));
                    x += 0.45 * u + slat;
                    break;
                }

                case BackgroundKind.VerticalSlats:
                    items.Add(Outlined(Box(x, sy + 0.4 * u, 0.4 * u, height - 0.8 * u, s.BackgroundColor), Ink));
                    anchors.Add(new Point(x + 0.2 * u, sy + height - 0.8 * u));
                    x += 0.4 * u;
                    break;
            }

            // Distansiya
            double spacer = OnSpacers ? 0.6 * u : 0;
            double y1 = sy + 1.5 * u;
            double y2 = sy + height - 1.5 * u;
            double mid = (y1 + y2) / 2;
            if (spacer > 0)
            {
                foreach (double by in new[] { y1 + u, y2 - u - 0.1 * u })
                    items.Add(Outlined(Box(x, by, spacer, 0.1 * u, Steel), Ink));
                anchors.Add(new Point(x + spacer / 2, y2 - u));
                x += spacer;
            }

            // Harf
            double lx = x;
            double depth = volumetric ? 2.2 * u : 0.35 * u;
            double wall = 0.12 * u;
            double faceT = 0.16 * u;

            if (!volumetric)
            {
                items.Add(Outlined(Box(lx, y1, depth, y2 - y1, s.FaceColor), Ink));
                anchors.Add(new Point(lx + depth / 2, mid));
            }
            else
            {
                bool halo = letter.Has(LightZones.Halo);
                items.Add(Outlined(Box(lx, y1, wall, y2 - y1, halo ? ClearAcrylic : Shade(s.ReturnColor, 0.85)), Ink));
                anchors.Add(new Point(lx + wall / 2, mid - 2 * u));

                WpfColor side = letter.Has(LightZones.SideFull) ? ClearAcrylic : s.ReturnColor;
                items.Add(Outlined(Box(lx, y2 - wall, depth, wall, side), Ink));
                items.Add(Outlined(Box(lx, y1, depth, wall, side), Ink));
                if (letter.Has(LightZones.SidePartial))
                {
                    items.Add(Box(lx + wall, y2 - wall, depth * 0.25, wall, ClearAcrylic));
                    items.Add(Box(lx + wall, y1, depth * 0.25, wall, ClearAcrylic));
                }
                anchors.Add(new Point(lx + depth / 2, y2 - wall / 2));

                items.Add(Outlined(Box(lx + depth - faceT, y1, faceT, y2 - y1, s.FaceColor), Ink));
                anchors.Add(new Point(lx + depth - faceT / 2, mid + 2 * u));

                if (letter.TrimMm > 0)
                {
                    foreach (double ty in new[] { y2 - 0.06 * u, y1 - 0.06 * u })
                        items.Add(Box(lx + depth - faceT - 0.25 * u, ty, faceT + 0.25 * u, 0.12 * u, s.ReturnColor));
                }

                if (letter.Has(LightZones.Dots))
                {
                    foreach (double dy in new[] { mid - 2 * u, mid, mid + 2 * u })
                        items.Add(Outlined(Circle(lx + depth + 0.08 * u, dy, 0.32 * u), Ink));
                }

                if (lit)
                {
                    double ledY = 0;
                    for (int i = 1; i <= 3; i++)
                    {
                        ledY = y1 + (y2 - y1) * i / 4;
                        items.Add(Outlined(Box(lx + wall, ledY - 0.15 * u, 0.22 * u, 0.3 * u, LedColor), Ink));
                    }
                    anchors.Add(new Point(lx + wall + 0.11 * u, ledY));
                }
            }

            // Nur yo'nalishi
            if (lit)
            {
                double ray = 1.1 * u;
                double rayWidth = th * 0.09;
                if (letter.Has(LightZones.Face) || letter.Has(LightZones.Dots) || letter.Has(LightZones.Pattern) || letter.Has(LightZones.Rim))
                {
                    foreach (double ry in new[] { mid - 1.6 * u, mid, mid + 1.6 * u })
                        items.Add(Line(lx + depth + 0.25 * u, ry, lx + depth + 0.25 * u + ray, ry + (ry - mid) * 0.35, RayColor, rayWidth));
                }
                if (letter.Has(LightZones.Halo))
                {
                    foreach (double ry in new[] { y1 + 0.5 * u, y2 - 0.5 * u })
                    {
                        double direction = ry > mid ? 1 : -1;
                        items.Add(Line(lx - 0.05 * u, ry, lx - Math.Max(spacer, 0.4 * u), ry + direction * 0.9 * u, RayColor, rayWidth));
                    }
                }
                if (letter.Has(LightZones.SideFull) || letter.Has(LightZones.SidePartial))
                {
                    double rx = letter.Has(LightZones.SidePartial) ? lx + wall + depth * 0.12 : lx + depth / 2;
                    items.Add(Line(rx, y2 + 0.15 * u, rx + 0.2 * u, y2 + 0.15 * u + ray * 0.8, RayColor, rayWidth));
                    items.Add(Line(rx, y1 - 0.15 * u, rx + 0.2 * u, y1 - 0.15 * u - ray * 0.8, RayColor, rayWidth));
                }
            }

            // Qalinlik o'lchami
            double dimY = y1 - 0.8 * u;
            double label = th * 0.8;
            items.AddRange(DimLine(lx, dimY, lx + depth, dimY, th * 0.3));
            items.Add(Label("D = " + Num.Format(volumetric ? s.Depth : Math.Min(s.Depth, 10)) + " mm", lx + depth / 2, dimY - label * 0.9, label, 0));
            if (spacer > 0)
            {
                double spacerY = dimY - label * 2.3;
                items.AddRange(DimLine(lx - spacer, spacerY, lx, spacerY, th * 0.3));
                items.Add(Label("30", lx - spacer / 2, spacerY - label * 0.9, label, 0));
            }

            // Yozuvlar (chiziq bilan bog'langan)
            List<string> labels = SectionLabels();
            double labelX = sx + height * 0.72;
            for (int i = 0; i < labels.Count && i < anchors.Count; i++)
            {
                double ly = sy + height - u * 0.4 - i * th * 1.6;
                items.Add(Line(anchors[i].X, anchors[i].Y, labelX - th * 0.3, ly, LeaderInk, th * 0.03));
                corel.Shape text = Label(labels[i], 0, 0, label, 0);
                text.CenterX = labelX + text.SizeWidth / 2;
                text.CenterY = ly;
                items.Add(text);
            }

            return items;
        }

        // ================================================================ o'lchamlar va sarlavha

        private List<corel.Shape> DrawDimensions()
        {
            var items = new List<corel.Shape>();
            double tick = th * 0.45;
            double left = ox + lay.Logo.X * k;
            double right = ox + (lay.Logo.X + lay.Logo.Width) * k;
            double top = oy + (lay.BackgroundHeight - lay.Logo.Y) * k;
            double bottom = top - lay.Logo.Height * k;

            // Logo eni — pastda
            double y = oy - th * 2.2;
            items.Add(Line(left, bottom, left, y - tick, Ink, th * 0.05));
            items.Add(Line(right, bottom, right, y - tick, Ink, th * 0.05));
            items.AddRange(DimLine(left, y, right, y, tick));
            items.Add(Label(Num.Format(s.LogoWidth) + " mm", (left + right) / 2, y - th * 0.9, th, 0));

            // Logo bo'yi — chapda
            double x = ox - th * 2.2;
            items.Add(Line(left, bottom, x - tick, bottom, Ink, th * 0.05));
            items.Add(Line(left, top, x - tick, top, Ink, th * 0.05));
            items.AddRange(DimLine(x, bottom, x, top, tick));
            items.Add(Label(Num.Format(s.LogoHeight) + " mm", x - th * 0.9, (bottom + top) / 2, th, 90));

            if (HasBackgroundDims)
            {
                double by = oy - th * 4.8;
                items.Add(Line(ox, oy, ox, by - tick, Ink, th * 0.05));
                items.Add(Line(ox + viewW, oy, ox + viewW, by - tick, Ink, th * 0.05));
                items.AddRange(DimLine(ox, by, ox + viewW, by, tick));
                items.Add(Label(Num.Format(lay.BackgroundWidth) + " mm", ox + viewW / 2, by - th * 0.9, th, 0));

                double bx = ox - th * 4.8;
                items.Add(Line(ox, oy, bx - tick, oy, Ink, th * 0.05));
                items.Add(Line(ox, oy + viewH, bx - tick, oy + viewH, Ink, th * 0.05));
                items.AddRange(DimLine(bx, oy, bx, oy + viewH, tick));
                items.Add(Label(Num.Format(lay.BackgroundHeight) + " mm", bx - th * 0.9, oy + viewH / 2, th, 90));
            }

            return items;
        }

        private List<string> TitleLines()
        {
            var lines = new List<string>
            {
                s.Letter.Name + " / " + s.Letter.EnglishName,
                string.Format("Fon: {0} · Logo {1} × {2} mm · qalinligi {3} mm · M 1:{4}",
                    BackgroundTitle(), Num.Format(s.LogoWidth), Num.Format(s.LogoHeight), Num.Format(s.Depth), s.Scale)
            };
            lines.AddRange(rules.Warnings.Select(w => "! " + w));
            return lines;
        }

        private string BackgroundTitle()
        {
            string title = s.Background.Name;
            if (s.Background.HasPattern)
                title += " (" + s.Pattern.Name + ")";
            if (s.Background.SupportsLighting && s.BackgroundLight != BackgroundLight.None)
                title += ", " + BackgroundLights.Name(s.BackgroundLight).ToLowerInvariant();
            return title;
        }

        private List<corel.Shape> DrawCaptions(Frame frame)
        {
            var items = new List<corel.Shape>();
            double captionY = oy + frame.CaptionY;
            items.Add(Label("KUNDUZI", ox + viewW / 2, captionY, th, 0));
            if (s.DrawNight)
            {
                string night = rules.LettersLit || rules.BackgroundLit ? "KECHASI" : "KECHASI (yoritilmaydi)";
                items.Add(Label(night, ox + frame.NightX + viewW / 2, captionY, th, 0));
            }
            if (s.DrawSection)
                items.Add(Label("YON KESIM (sxema)", ox + frame.SectionX + frame.SectionHeight * 0.35, captionY, th, 0));

            List<string> lines = TitleLines();
            for (int i = 0; i < lines.Count; i++)
            {
                double size = i == 0 ? th * 1.3 : th * 0.95;
                corel.Shape text = Label(lines[i], 0, 0, size, 0, i >= 2 ? WarningInk : Ink);
                text.CenterX = ox + frame.MinX + text.SizeWidth / 2;
                text.CenterY = oy + frame.TitleY + (lines.Count - 1 - i) * th * 1.9;
                items.Add(text);
            }
            return items;
        }

        // ================================================================ nuqtalar va naqsh

        /// <summary>Kontur ichidagi to'r nuqtalari (chetdan clearance masofada).</summary>
        private List<Point> InsidePoints(double spacing, double clearance, bool staggered, out double scale)
        {
            double width = silhouette.SizeWidth;
            double height = silhouette.SizeHeight;
            double left = silhouette.CenterX - width / 2;
            double bottom = silhouette.CenterY - height / 2;

            // Juda ko'p nuqta bo'lsa, oraliqni kattalashtiramiz
            scale = 1;
            while ((width / (spacing * scale)) * (height / (spacing * scale)) > MaxDots * 2.5)
                scale *= 1.2;
            double step = spacing * scale;
            double clear = clearance * scale;

            var points = new List<Point>();
            int row = 0;
            for (double y = bottom + step / 2; y < bottom + height; y += step * (staggered ? 0.866 : 1), row++)
            {
                double shift = staggered && row % 2 == 1 ? step / 2 : 0;
                for (double x = left + step / 2 + shift; x < left + width; x += step)
                {
                    if (Inside(x, y) && Inside(x - clear, y) && Inside(x + clear, y) && Inside(x, y - clear) && Inside(x, y + clear))
                        points.Add(new Point(x, y));
                }
            }
            return points;
        }

        private bool Inside(double x, double y)
        {
            // Natija turi versiyalarda farq qilishi mumkin, shuning uchun nomi bo'yicha solishtiramiz
            object position = silhouette.IsOnShape(x, y);
            return position != null && position.ToString() == "cdrInsideShape";
        }

        private corel.Shape Circles(List<Point> points, double diameter)
        {
            if (points.Count == 0)
                return null;
            corel.ShapeRange range = app.CreateShapeRange();
            foreach (Point p in points)
                range.Add(Temp(layer.CreateEllipse2(p.X, p.Y, diameter / 2, diameter / 2)));
            return range.Count > 1 ? range.Combine() : range.FirstShape;
        }

        private corel.Shape Diamonds(List<Point> points, double side)
        {
            if (points.Count == 0)
                return null;
            corel.ShapeRange range = app.CreateShapeRange();
            foreach (Point p in points)
            {
                corel.Shape square = Temp(layer.CreateRectangle2(p.X - side / 2, p.Y - side / 2, side, side));
                square.Rotate(45);
                range.Add(square);
            }
            return range.Count > 1 ? range.Combine() : range.FirstShape;
        }

        // ================================================================ yordamchilar

        private corel.Shape Final(List<corel.Shape> items, string name)
        {
            corel.ShapeRange range = app.CreateShapeRange();
            foreach (corel.Shape item in items)
            {
                if (item != null)
                    range.Add(item);
            }
            corel.Shape group = range.Count > 1 ? range.Group() : range.FirstShape;
            group.Name = name;
            Track(group);
            finals.Add(group);
            return group;
        }

        /// <summary>Andozadan nusxa (yangi obyekt har doim eng ustiga chiqadi — chizish tartibi saqlanadi).</summary>
        private corel.Shape Dup(corel.Shape source, double dx, double dy)
        {
            corel.Shape copy = Track(source.Duplicate(dx, dy));
            copy.OrderToFront();
            return copy;
        }

        private corel.Shape Box(double x, double y, double width, double height, WpfColor color)
        {
            corel.Shape shape = Track(layer.CreateRectangle2(x, y, Math.Max(width, 0.01), Math.Max(height, 0.01)));
            Fill(shape, color);
            shape.Outline.SetNoOutline();
            return shape;
        }

        private corel.Shape Circle(double cx, double cy, double diameter)
        {
            corel.Shape shape = Track(layer.CreateEllipse2(cx, cy, diameter / 2, diameter / 2));
            Fill(shape, Steel);
            shape.Outline.SetNoOutline();
            return shape;
        }

        private corel.Shape Outlined(corel.Shape shape, WpfColor color)
        {
            Stroke(shape, Num.Clamp(th * 0.04, 0.1, 2), color);
            return shape;
        }

        private corel.Shape Line(double x1, double y1, double x2, double y2, WpfColor color, double width)
        {
            corel.Shape line = Track(layer.CreateLineSegment(x1, y1, x2, y2));
            line.Outline.Width = Math.Max(width, 0.05);
            line.Outline.Color.RGBAssign(color.R, color.G, color.B);
            return line;
        }

        /// <summary>O'lcham chizig'i: asosiy chiziq va ikki uchida qiya chiziqcha.</summary>
        private IEnumerable<corel.Shape> DimLine(double x1, double y1, double x2, double y2, double tick)
        {
            yield return Line(x1, y1, x2, y2, Ink, th * 0.05);
            yield return Line(x1 - tick / 2, y1 - tick / 2, x1 + tick / 2, y1 + tick / 2, Ink, th * 0.1);
            yield return Line(x2 - tick / 2, y2 - tick / 2, x2 + tick / 2, y2 + tick / 2, Ink, th * 0.1);
        }

        private corel.Shape Label(string text, double centerX, double centerY, double heightMm, double angle, WpfColor? color = null)
        {
            corel.Shape label = Track(layer.CreateArtisticText(centerX, centerY, text));
            label.Text.Story.Font = "Arial";
            label.Text.Story.Size = (float)(heightMm * PointsPerMm);
            Fill(label, color ?? Ink);
            if (angle != 0)
                label.Rotate(angle);
            label.CenterX = centerX;
            label.CenterY = centerY;
            return label;
        }

        private static void Fill(corel.Shape shape, WpfColor color)
        {
            shape.Fill.UniformColor.RGBAssign(color.R, color.G, color.B);
        }

        private void Metallic(corel.Shape shape, WpfColor color)
        {
            WpfColor dark = Shade(color, 0.72);
            WpfColor bright = Shade(color, 1.25);
            shape.Fill.ApplyFountainFill(app.CreateRGBColor(dark.R, dark.G, dark.B), app.CreateRGBColor(bright.R, bright.G, bright.B),
                corel.cdrFountainFillType.cdrLinearFountainFill, 90);
        }

        private static void Stroke(corel.Shape shape, double width, WpfColor color)
        {
            shape.Outline.Width = Math.Max(width, 0.05);
            shape.Outline.Color.RGBAssign(color.R, color.G, color.B);
            try
            {
                // Dumaloq burchak — nur va qalin konturda o'tkir "tikan" bo'lmasin (1 = cdrOutlineRoundLineJoin)
                ((dynamic)shape.Outline).LineJoin = 1;
            }
            catch
            {
                // Eski versiyada bo'lmasa — oddiy burchak qoladi
            }
        }

        private corel.Shape Track(corel.Shape shape)
        {
            everything.Add(shape);
            return shape;
        }

        private corel.Shape Temp(corel.Shape shape)
        {
            if (shape != null)
                temporaries.Add(shape);
            return shape;
        }

        private static void DeleteAll(List<corel.Shape> shapes)
        {
            for (int i = shapes.Count - 1; i >= 0; i--)
            {
                try
                {
                    shapes[i].Delete();
                }
                catch
                {
                    // Allaqachon guruhga qo'shilgan yoki birlashtirilgan
                }
            }
            shapes.Clear();
        }

        private static WpfColor Rgb(int hex) => ColorMath.Rgb(hex);

        private static WpfColor Shade(WpfColor color, double factor) => ColorMath.Shade(color, factor);

        private static WpfColor Lit(WpfColor color) => ColorMath.Lit(color);
    }
}
