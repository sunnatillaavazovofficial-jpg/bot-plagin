using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Rendering
{
    /// <summary>
    /// Paneldagi jonli ko'rinish (kunduzi / kechasi). Logo rasmi niqob (mask) sifatida ishlatiladi:
    /// shu tufayli yuz, bort, nur va nuqtalarni istalgan rangda ko'rsatish mumkin.
    /// </summary>
    public static class PreviewBuilder
    {
        private const double Size = 420; // eng katta tomon, px

        private static BitmapSource placeholder;

        public static FrameworkElement Build(SignSettings s, RuleResult rules, LogoInfo logo, bool night)
        {
            SignLayout lay = LogoLayout.Compute(s);
            double px = Size / Math.Max(lay.BackgroundWidth, lay.BackgroundHeight);

            var root = new Grid
            {
                Width = lay.BackgroundWidth * px,
                Height = lay.BackgroundHeight * px,
                ClipToBounds = false
            };
            root.Children.Add(Background(s, rules, night, root.Width, root.Height, px));

            BitmapSource mask = logo?.Preview ?? Placeholder();
            var letters = new Grid
            {
                Width = Math.Max(1, lay.Logo.Width * px),
                Height = Math.Max(1, lay.Logo.Height * px),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(lay.Logo.X * px, lay.Logo.Y * px, 0, 0)
            };
            AddLetters(letters, s, rules, mask, logo?.Preview, night, px);
            root.Children.Add(letters);
            return root;
        }

        private static UIElement Background(SignSettings s, RuleResult rules, bool night, double width, double height, double px)
        {
            Color color = s.BackgroundColor;
            bool lit = night && rules.BackgroundLit;
            Color surface = night ? (lit ? ColorMath.Lit(color) : ColorMath.Shade(color, 0.22)) : color;
            double minSide = Math.Min(width, height) / px;
            var canvas = new Grid { Width = width, Height = height };

            switch (s.Background.Kind)
            {
                case BackgroundKind.Wall:
                    canvas.Children.Add(new Rectangle { Fill = Brush(surface) });
                    break;

                case BackgroundKind.Panel:
                    canvas.Children.Add(new Rectangle { Fill = Brush(surface), Stroke = Brush(ColorMath.Shade(surface, 0.75)), StrokeThickness = 1 });
                    break;

                case BackgroundKind.Cassette:
                {
                    double fold = Num.Clamp(minSide * 0.02, 10, 30) * px;
                    canvas.Children.Add(new Rectangle { Fill = Brush(surface), Stroke = Brush(ColorMath.Shade(surface, 0.6)), StrokeThickness = 1 });
                    canvas.Children.Add(new Rectangle { Margin = new Thickness(fold), Stroke = Brush(ColorMath.Shade(surface, 0.7)), StrokeThickness = 1 });
                    break;
                }

                case BackgroundKind.Lightbox:
                {
                    double profile = SignDetails.SizeOf(s, Detail.Frame) * px;
                    Color profileColor = SignDetails.ColorOf(s, Detail.Frame);
                    if (profile > 0)
                        canvas.Children.Add(new Rectangle { Fill = night ? Brush(ColorMath.Shade(profileColor, 0.3)) : Metal(profileColor) });
                    var face = new Rectangle { Margin = new Thickness(profile), Fill = Brush(surface) };
                    if (lit)
                        face.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 18, Color = surface, Opacity = 0.8 };
                    canvas.Children.Add(face);
                    break;
                }

                case BackgroundKind.AcrylicPlate:
                {
                    canvas.Children.Add(new Rectangle
                    {
                        Fill = Brush(surface),
                        Opacity = night ? 1 : 0.8,
                        Stroke = Brush(ColorMath.Shade(surface, 0.7)),
                        StrokeThickness = 1
                    });
                    double d = SignDetails.SizeOf(s, Detail.Bolts) * px;
                    Color boltColor = SignDetails.ColorOf(s, Detail.Bolts);
                    Brush bolt = Brush(night ? ColorMath.Shade(boltColor, 0.3) : boltColor);
                    foreach (HorizontalAlignment h in new[] { HorizontalAlignment.Left, HorizontalAlignment.Right })
                    {
                        foreach (VerticalAlignment v in new[] { VerticalAlignment.Top, VerticalAlignment.Bottom })
                        {
                            canvas.Children.Add(new Ellipse
                            {
                                Width = d,
                                Height = d,
                                Fill = bolt,
                                HorizontalAlignment = h,
                                VerticalAlignment = v,
                                Margin = new Thickness(d * 1.1),
                                Stroke = Brush(ColorMath.Shade(boltColor, 0.7)),
                                StrokeThickness = 0.5
                            });
                        }
                    }
                    break;
                }

                case BackgroundKind.PatternSheet:
                case BackgroundKind.FramedMesh:
                case BackgroundKind.WoodFrameMesh:
                    canvas.Children.Add(PatternBackground(s, rules, night, px));
                    break;

                case BackgroundKind.HorizontalSlats:
                case BackgroundKind.VerticalSlats:
                    canvas.Children.Add(SlatBackground(s, rules, night, px));
                    break;

                case BackgroundKind.AcpBox:
                    canvas.Children.Add(new Rectangle
                    {
                        Fill = Brush(night ? ColorMath.Shade(color, 0.22) : color),
                        Stroke = Brush(ColorMath.Shade(color, 0.55)),
                        StrokeThickness = 1
                    });
                    break;
            }

            if (s.EngravedBorder && s.Background.CanHaveBorder)
            {
                Color borderColor = SignDetails.ColorOf(s, Detail.Border);
                double line = SignDetails.SizeOf(s, Detail.Border) * px;
                canvas.Children.Add(new Rectangle
                {
                    // Corel chizmasidagidek chiziq o'rtasi chetdan "inset" masofada
                    Margin = new Thickness(Math.Max(SignDetails.SizeOf(s, Detail.BorderInset) * px - line / 2, 0)),
                    Stroke = Brush(night ? ColorMath.Shade(borderColor, 0.3) : borderColor),
                    StrokeThickness = Math.Max(line, 1)
                });
            }
            return canvas;
        }

        /// <summary>Gulli list / setka (Corel chizmasi bilan bir xil hisob).</summary>
        private static UIElement PatternBackground(SignSettings s, RuleResult rules, bool night, double px)
        {
            SignLayout lay = LogoLayout.Compute(s);
            BackgroundStyle background = s.Background;
            var canvas = new Canvas { Width = lay.BackgroundWidth * px, Height = lay.BackgroundHeight * px };
            var full = new Rect(0, 0, lay.BackgroundWidth, lay.BackgroundHeight);
            bool lit = night && rules.BackgroundLit;
            double frameWidth = SignDetails.SizeOf(s, Detail.Frame);
            Color frameColor = SignDetails.ColorOf(s, Detail.Frame);
            Color light = SignDetails.ColorOf(s, Detail.BackgroundLight);
            var inner = new Rect(frameWidth, frameWidth, Math.Max(full.Width - 2 * frameWidth, 1), Math.Max(full.Height - 2 * frameWidth, 1));

            if (frameWidth > 0)
                AddRect(canvas, full, px, night ? Brush(ColorMath.Shade(frameColor, 0.3)) : Metal(frameColor));
            Color sheet = night ? ColorMath.Shade(s.BackgroundColor, lit ? 0.45 : 0.22) : s.BackgroundColor;
            AddRect(canvas, inner, px, Brush(sheet));

            Rect holeArea = inner;
            if (background.Kind == BackgroundKind.PatternSheet)
            {
                double edge = Num.Clamp(Math.Min(inner.Width, inner.Height) * 0.03, 15, 40);
                holeArea = new Rect(inner.X + edge, inner.Y + edge, Math.Max(inner.Width - 2 * edge, 1), Math.Max(inner.Height - 2 * edge, 1));
            }
            var holes = PatternGeometry.Holes(SignDetails.Pattern(s), holeArea, out _);
            Color behind = s.BackgroundLight != BackgroundLight.None ? ColorMath.Diffuser : ColorMath.Shade(SignDetails.ColorOf(s, Detail.Wall), 0.75);
            if (!lit)
            {
                canvas.Children.Add(Polygons(holes.ConvertAll(h => h.Points), px, Brush(night ? ColorMath.Shade(behind, 0.15) : behind)));
            }
            else
            {
                for (int level = 0; level < PatternGeometry.LightLevels; level++)
                {
                    var group = holes.FindAll(h => PatternGeometry.Level(s.BackgroundLight, h.Center, full, lay.Logo) == level).ConvertAll(h => h.Points);
                    Color color = ColorMath.LightLevel(behind, light, level, PatternGeometry.LightLevels);
                    var path = Polygons(group, px, Brush(color));
                    if (level == PatternGeometry.LightLevels - 1)
                        path.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 10, Color = light, Opacity = 0.9 };
                    canvas.Children.Add(path);
                }
            }

            if (background.Kind == BackgroundKind.FramedMesh && frameWidth > 0 && inner.Width > 1500)
            {
                int bars = (int)Math.Round(inner.Width / 1000) - 1;
                for (int i = 1; i <= bars; i++)
                {
                    double x = inner.X + inner.Width * i / (bars + 1);
                    AddRect(canvas, new Rect(x - frameWidth * 0.4, inner.Y, frameWidth * 0.8, inner.Height), px,
                        Brush(night ? ColorMath.Shade(frameColor, 0.3) : frameColor));
                }
            }
            return canvas;
        }

        /// <summary>Gorizontal reykalar (kronshteynda) yoki vertikal reykali panel.</summary>
        private static UIElement SlatBackground(SignSettings s, RuleResult rules, bool night, double px)
        {
            SignLayout lay = LogoLayout.Compute(s);
            var canvas = new Canvas { Width = lay.BackgroundWidth * px, Height = lay.BackgroundHeight * px };
            var full = new Rect(0, 0, lay.BackgroundWidth, lay.BackgroundHeight);
            bool horizontal = s.Background.Kind == BackgroundKind.HorizontalSlats;
            bool lit = night && rules.BackgroundLit;
            Color slatColor = night ? ColorMath.Shade(s.BackgroundColor, 0.25) : s.BackgroundColor;
            Color wall = SignDetails.ColorOf(s, Detail.Wall);
            var slats = PatternGeometry.Slats(full, horizontal, SignDetails.SizeOf(s, Detail.SlatWidth), SignDetails.SizeOf(s, Detail.SlatGap));

            if (horizontal)
            {
                AddRect(canvas, full, px, Brush(night ? ColorMath.Shade(wall, 0.18) : wall));
                int posts = Math.Max(2, (int)Math.Round(full.Width / 1200) + 1);
                for (int i = 0; i < posts; i++)
                {
                    double x = full.Width * (i + 0.5) / posts;
                    AddRect(canvas, new Rect(x - 20, 0, 40, full.Height), px, Brush(ColorMath.Shade(slatColor, 0.75)));
                }
            }
            else
            {
                AddRect(canvas, full, px, Brush(night ? ColorMath.Shade(s.BackgroundColor, 0.12) : ColorMath.Shade(s.BackgroundColor, 0.55)));
            }

            if (lit)
            {
                var gaps = PatternGeometry.SlatGaps(slats, horizontal, 16);
                for (int level = 1; level < PatternGeometry.LightLevels; level++)
                {
                    var group = gaps.FindAll(g => PatternGeometry.Level(s.BackgroundLight, new Point(g.X + g.Width / 2, g.Y + g.Height / 2), full, lay.Logo) == level)
                                    .ConvertAll(g => new[] { g.TopLeft, g.TopRight, g.BottomRight, g.BottomLeft });
                    var path = Polygons(group, px, Brush(ColorMath.LightLevel(wall, SignDetails.ColorOf(s, Detail.BackgroundLight), level, PatternGeometry.LightLevels)));
                    path.Effect = new BlurEffect { Radius = 6 };
                    canvas.Children.Add(path);
                }
            }

            foreach (Rect slat in slats)
                AddRect(canvas, slat, px, !night && horizontal ? Metal(slatColor) : Brush(slatColor));
            return canvas;
        }

        private static void AddRect(Canvas canvas, Rect r, double px, Brush fill)
        {
            var rectangle = new Rectangle { Width = Math.Max(r.Width * px, 0.1), Height = Math.Max(r.Height * px, 0.1), Fill = fill };
            Canvas.SetLeft(rectangle, r.X * px);
            Canvas.SetTop(rectangle, r.Y * px);
            canvas.Children.Add(rectangle);
        }

        private static Path Polygons(System.Collections.Generic.List<Point[]> polygons, double px, Brush fill)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                foreach (Point[] polygon in polygons)
                {
                    context.BeginFigure(new Point(polygon[0].X * px, polygon[0].Y * px), true, true);
                    for (int i = 1; i < polygon.Length; i++)
                        context.LineTo(new Point(polygon[i].X * px, polygon[i].Y * px), false, false);
                }
            }
            geometry.Freeze();
            return new Path { Data = geometry, Fill = fill, IsHitTestVisible = false };
        }

        /// <summary>Gul tanlash ro'yxati uchun kichik rasm.</summary>
        public static ImageSource PatternThumbnail(PatternStyle pattern)
        {
            var area = new Rect(0, 0, pattern.Pitch * 6, pattern.Pitch * 4);
            var holes = PatternGeometry.Holes(pattern, area, out _);
            var geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                foreach (Hole hole in holes)
                {
                    context.BeginFigure(hole.Points[0], true, true);
                    for (int i = 1; i < hole.Points.Length; i++)
                        context.LineTo(hole.Points[i], false, false);
                }
            }
            geometry.Freeze();

            var drawing = new DrawingGroup();
            drawing.Children.Add(new GeometryDrawing(Brush(ColorMath.Rgb(0xF2F2F2)), null, new RectangleGeometry(area)));
            drawing.Children.Add(new GeometryDrawing(Brush(ColorMath.Rgb(0x3D5566)), null, geometry));
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

        /// <summary>Fonga o'yilgan logo (Corel chizmasi bilan bir xil mantiq).</summary>
        private static void AddCutLogo(Grid grid, SignSettings s, RuleResult rules, BitmapSource mask, BitmapSource original, bool night, double px)
        {
            LetterStyle letter = s.Letter;
            bool lit = night && rules.LettersLit;
            Color light = s.LightColor;
            double glow = SignDetails.SizeOf(s, Detail.Glow) * px;
            CutMode mode = letter.Cut == CutMode.PushThrough && !rules.CanBe3D ? CutMode.RoutedLit : letter.Cut;
            Color edge = ColorMath.Shade(s.BackgroundColor, 0.45);

            if (mode == CutMode.PushThrough)
            {
                if (!night)
                {
                    double offset = Num.Clamp(SignDetails.SizeOf(s, Detail.Protrusion) * 0.35, 3, 20) * px;
                    UIElement shadow = Silhouette(mask, Brush(Colors.Black), new BlurEffect { Radius = Math.Max(1, offset * 0.6) });
                    shadow.Opacity = 0.3;
                    shadow.RenderTransform = new TranslateTransform(offset, offset);
                    grid.Children.Add(shadow);
                }
                if (lit)
                    grid.Children.Add(Silhouette(mask, Brush(light), new BlurEffect { Radius = glow * 0.8 }));
                AddOutline(grid, mask, lit ? light : (night ? ColorMath.Shade(s.ReturnColor, 0.25) : s.ReturnColor),
                    Math.Max(SignDetails.SizeOf(s, Detail.Trim) * px, 0.8));
            }
            else
            {
                AddOutline(grid, mask, night ? ColorMath.Shade(edge, 0.4) : edge, Math.Max(1.5 * px, 0.6));
            }

            bool faceLit = lit && (mode == CutMode.RoutedLit || mode == CutMode.PushThrough);
            FrameworkElement face;
            if (s.KeepLogoColors && original != null && mode == CutMode.RoutedLit && (!night || faceLit))
            {
                face = new Image { Source = original, Stretch = Stretch.Fill };
            }
            else
            {
                Color color = night ? (faceLit ? ColorMath.Lit(s.FaceColor) : ColorMath.Shade(s.FaceColor, 0.22)) : s.FaceColor;
                face = Silhouette(mask, !night && mode == CutMode.Engraved ? Metal(color) : Brush(color), null);
            }
            if (faceLit)
                face.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = glow * 0.7, Color = ColorMath.Lit(s.FaceColor), Opacity = 1 };
            grid.Children.Add(face);
        }

        /// <summary>Logo atrofida chiziq: siljitilgan nusxalar.</summary>
        private static void AddOutline(Grid grid, BitmapSource mask, Color color, double width)
        {
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                UIElement copy = Silhouette(mask, Brush(color), null);
                copy.RenderTransform = new TranslateTransform(Math.Cos(angle) * width, Math.Sin(angle) * width);
                grid.Children.Add(copy);
            }
        }

        private static void AddLetters(Grid grid, SignSettings s, RuleResult rules, BitmapSource mask, BitmapSource original, bool night, double px)
        {
            if (s.Letter.IsCut)
            {
                AddCutLogo(grid, s, rules, mask, original, night, px);
                return;
            }

            LetterStyle letter = s.Letter;
            bool volumetric = rules.CanBe3D;
            bool lit = night && rules.LettersLit;
            Color light = s.LightColor;
            double glow = SignDetails.SizeOf(s, Detail.Glow) * px;

            // 0. Kunduzi — fonga tushgan soya (Corel chizmasidagi bilan bir xil siljish)
            if (!night && volumetric)
            {
                double offset = Num.Clamp((s.Depth + (letter.OnStandoffs ? SignDetails.SizeOf(s, Detail.Standoff) : 0)) * 0.3, 4, 60) * px;
                UIElement shadow = Silhouette(mask, Brush(Colors.Black), new BlurEffect { Radius = Math.Max(1, offset * 0.6) });
                shadow.Opacity = 0.3;
                shadow.RenderTransform = new TranslateTransform(offset, offset);
                grid.Children.Add(shadow);
            }

            // 1. Nur (eng orqada)
            if (lit)
            {
                if (letter.Has(LightZones.Halo))
                {
                    grid.Children.Add(Silhouette(mask, Brush(light), new BlurEffect { Radius = glow * 1.6 }));
                    grid.Children.Add(Silhouette(mask, Brush(light), new BlurEffect { Radius = glow * 0.7 }));
                }
                if (letter.Has(LightZones.SideFull) || letter.Has(LightZones.Rim) || letter.Has(LightZones.SidePartial))
                {
                    double radius = letter.Has(LightZones.SidePartial) ? glow * 0.4 : glow * 0.8;
                    grid.Children.Add(Silhouette(mask, Brush(light), new BlurEffect { Radius = radius }));
                }
            }

            // 2. Chet: bort qirrasi yoki yonadigan kontur (siljitilgan nusxalar)
            double trim = volumetric ? SignDetails.SizeOf(s, Detail.Trim) * px : 0;
            bool rimLit = lit && (letter.Has(LightZones.SideFull) || letter.Has(LightZones.SidePartial) || letter.Has(LightZones.Rim));
            if (rimLit || trim > 0)
            {
                Color rimColor = rimLit ? light : (night ? ColorMath.Shade(s.ReturnColor, 0.25) : s.ReturnColor);
                double width = Math.Max(rimLit ? Math.Max(trim, 3 * px) : trim, 0.8);
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4;
                    UIElement copy = Silhouette(mask, Brush(rimColor), null);
                    copy.RenderTransform = new TranslateTransform(Math.Cos(angle) * width, Math.Sin(angle) * width);
                    grid.Children.Add(copy);
                }
            }

            // 3. Yuz
            bool faceLit = lit && letter.Has(LightZones.Face);
            FrameworkElement face;
            if (s.KeepLogoColors && original != null && (!night || faceLit))
            {
                face = new Image { Source = original, Stretch = Stretch.Fill };
            }
            else
            {
                Color color = night ? (faceLit ? ColorMath.Lit(s.FaceColor) : ColorMath.Shade(s.FaceColor, 0.22)) : s.FaceColor;
                Brush brush = !night && letter.Face == FaceMaterial.Metal ? Metal(color) : Brush(color);
                face = Silhouette(mask, brush, null);
                if (!night && letter.Face == FaceMaterial.ClearAcrylic && volumetric)
                    face.Opacity = 0.88;
            }
            if (faceLit)
                face.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = glow * 0.9, Color = ColorMath.Lit(s.FaceColor), Opacity = 1 };
            grid.Children.Add(face);

            if (!volumetric)
                return;

            // 4. Nuqtalar va naqsh
            if (letter.Has(LightZones.Dots))
            {
                double spacing = SignDetails.DotSpacing(s) * px;
                double diameter = SignDetails.SizeOf(s, Detail.Dots) * px;
                Color dayColor = SignDetails.ColorOf(s, Detail.Dots);
                bool dotsLit = lit;
                Color color = dotsLit ? light : (night ? ColorMath.Shade(dayColor, 0.25) : dayColor);
                Geometry dot = new EllipseGeometry(new Point(spacing / 2, spacing / 2), diameter / 2, diameter / 2);
                var dots = Silhouette(mask, Tile(dot, color, spacing), null);
                if (dotsLit)
                    dots.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = diameter * 2, Color = light, Opacity = 1 };
                grid.Children.Add(dots);
            }

            if (letter.Has(LightZones.Pattern))
            {
                double spacing = SignDetails.FacePatternSpacing(s) * px;
                double half = SignDetails.SizeOf(s, Detail.FacePattern) * px * 0.707;
                double c = spacing / 2;
                Geometry diamond = Geometry.Parse(string.Format(CultureInfo.InvariantCulture,
                    "M{0},{1} L{2},{0} {0},{3} {1},{0}Z", c, c - half, c + half, c + half));
                Color dayColor = SignDetails.ColorOf(s, Detail.FacePattern);
                Color color = lit ? light : (night ? ColorMath.Shade(dayColor, 0.25) : dayColor);
                var pattern = Silhouette(mask, Tile(diamond, color, spacing), null);
                if (lit)
                    pattern.Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = half, Color = light, Opacity = 1 };
                grid.Children.Add(pattern);
            }
        }

        /// <summary>Logo shaklidagi bo'yalgan to'rtburchak (logo rasmi niqob sifatida).</summary>
        private static Rectangle Silhouette(BitmapSource mask, Brush fill, Effect effect)
        {
            return new Rectangle
            {
                Fill = fill,
                OpacityMask = new ImageBrush(mask) { Stretch = Stretch.Fill },
                Effect = effect,
                IsHitTestVisible = false
            };
        }

        private static Brush Tile(Geometry shape, Color color, double spacing)
        {
            var brush = new DrawingBrush(new GeometryDrawing(Brush(color), null, shape))
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, spacing, spacing),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, spacing, spacing),
                ViewboxUnits = BrushMappingMode.Absolute
            };
            brush.Freeze();
            return brush;
        }

        private static SolidColorBrush Brush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Brush Metal(Color color)
        {
            var brush = new LinearGradientBrush(ColorMath.Shade(color, 1.25), ColorMath.Shade(color, 0.72), 90);
            brush.Freeze();
            return brush;
        }

        /// <summary>Logo tanlanmaganda ko'rsatiladigan "LOGO" yozuvi.</summary>
        private static BitmapSource Placeholder()
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
            placeholder = bitmap;
            return placeholder;
        }
    }
}
