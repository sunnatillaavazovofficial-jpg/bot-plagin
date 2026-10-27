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
            Color steel = ColorMath.Rgb(0xA3A9AF);
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
                    double profile = Num.Clamp(minSide * 0.03, 25, 60) * px;
                    canvas.Children.Add(new Rectangle { Fill = night ? Brush(ColorMath.Shade(steel, 0.3)) : Metal(steel) });
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
                    double d = Num.Clamp(minSide * 0.03, 20, 40) * px;
                    Brush bolt = Brush(night ? ColorMath.Shade(steel, 0.3) : steel);
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
                                Margin = new Thickness(d * 1.1)
                            });
                        }
                    }
                    break;
                }
            }
            return canvas;
        }

        private static void AddLetters(Grid grid, SignSettings s, RuleResult rules, BitmapSource mask, BitmapSource original, bool night, double px)
        {
            LetterStyle letter = s.Letter;
            bool volumetric = rules.CanBe3D;
            bool lit = night && rules.LettersLit;
            Color light = s.LightColor;
            double glow = Num.Clamp(s.LogoHeight * 0.06, 15, 150) * px;

            // 0. Kunduzi — fonga tushgan soya (Corel chizmasidagi bilan bir xil siljish)
            if (!night && volumetric)
            {
                double offset = Num.Clamp((s.Depth + (letter.OnStandoffs ? 30 : 0)) * 0.3, 4, 60) * px;
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
            double trim = volumetric ? letter.TrimMm * px : 0;
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
                double spacing, diameter;
                Color dayColor;
                if (letter.Face == FaceMaterial.Perforated)
                {
                    spacing = Num.Clamp(s.LogoHeight * 0.03, 8, 30) * px;
                    diameter = spacing * 0.5;
                    dayColor = ColorMath.Shade(s.FaceColor, 2.4);
                }
                else if (letter.Face == FaceMaterial.Metal)
                {
                    spacing = Num.Clamp(s.LogoHeight * 0.12, 40, 200) * px;
                    diameter = spacing * 0.55;
                    dayColor = ColorMath.Rgb(0xF3EBD8);
                }
                else
                {
                    spacing = Num.Clamp(s.LogoHeight * 0.07, 20, 100) * px;
                    diameter = spacing * 0.35;
                    dayColor = ColorMath.Rgb(0xD9D9D9);
                }
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
                double spacing = Num.Clamp(s.LogoHeight * 0.08, 25, 120) * px;
                double half = spacing * 0.35;
                double c = spacing / 2;
                Geometry diamond = Geometry.Parse(string.Format(CultureInfo.InvariantCulture,
                    "M{0},{1} L{2},{0} {0},{3} {1},{0}Z", c, c - half, c + half, c + half));
                Color dayColor = ColorMath.Rgb(0xE8C766);
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
