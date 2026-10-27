using System;
using System.Collections.Generic;
using System.Windows;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Core
{
    /// <summary>Fon yoritilishi — qaysi joyi yonadi.</summary>
    public enum BackgroundLight
    {
        None,

        /// <summary>Butun fon orqadan bir tekis yonadi.</summary>
        Full,

        /// <summary>Chetlari bo'ylab yonadi, markazga qarab so'nadi (ramka ichidagi LED lenta).</summary>
        Perimeter,

        /// <summary>Faqat logo atrofi yonadi.</summary>
        AroundLogo,

        /// <summary>Pastdan yuqoriga qarab so'nadi.</summary>
        Bottom
    }

    /// <summary>Fon yoritilishi nomlari (panel uchun) va tavsifi (ChatGPT uchun).</summary>
    public static class BackgroundLights
    {
        public static readonly BackgroundLight[] All =
        {
            BackgroundLight.None, BackgroundLight.Full, BackgroundLight.Perimeter, BackgroundLight.AroundLogo, BackgroundLight.Bottom
        };

        public static string Name(BackgroundLight light)
        {
            switch (light)
            {
                case BackgroundLight.Full: return "Butunlay yonadi";
                case BackgroundLight.Perimeter: return "Chetlari bo'ylab yonadi";
                case BackgroundLight.AroundLogo: return "Logo atrofi yonadi";
                case BackgroundLight.Bottom: return "Pastdan yonadi";
                default: return "Yonmaydi";
            }
        }

        public static string Prompt(BackgroundLight light)
        {
            switch (light)
            {
                case BackgroundLight.Full: return "the background is evenly backlit by LEDs behind it, light shines through every opening";
                case BackgroundLight.Perimeter: return "the background is backlit only along its perimeter (LED strip inside the frame), bright at the edges and fading towards the centre";
                case BackgroundLight.AroundLogo: return "only the zone of the background around the logo is backlit, fading away from the logo";
                case BackgroundLight.Bottom: return "the background is lit from the bottom edge, fading upwards";
                default: return "the background itself is not illuminated";
            }
        }
    }

    /// <summary>Gulning bitta teshigi: ko'pburchak (mm, fonning chap-yuqori burchagidan, Y pastga).</summary>
    public sealed class Hole
    {
        public Hole(Point[] points, Point center)
        {
            Points = points;
            Center = center;
        }

        public Point[] Points { get; }
        public Point Center { get; }
    }

    /// <summary>
    /// Gul (perforatsiya, setka) va reykalar geometriyasi hamda fon yoritilishi.
    /// Corel chizmasi ham, panel ko'rinishi ham shu hisobdan foydalanadi.
    /// </summary>
    public static class PatternGeometry
    {
        /// <summary>Juda ko'p teshik bo'lsa Corel sekinlashadi — qadam kattalashtiriladi.</summary>
        public const int MaxHoles = 1600;

        /// <summary>Yorug'lik darajalari soni (kechki ko'rinishda).</summary>
        public const int LightLevels = 4;

        /// <summary>Berilgan to'rtburchak ichidagi teshiklar. scale — qadam necha marta kattalashtirilgani.</summary>
        public static List<Hole> Holes(PatternStyle p, Rect area, out double scale)
        {
            double stepX = p.Pitch;
            double stepY = RowStep(p);
            scale = 1;
            while (area.Width / (stepX * scale) * (area.Height / (stepY * scale)) > MaxHoles)
                scale *= 1.15;

            double pitch = p.Pitch * scale;
            double rowStep = stepY * scale;
            bool staggered = p.Shape != HoleShape.Square;
            var holes = new List<Hole>();

            // To'r markazdan quriladi — gul fon chetlarida simmetrik chiqadi
            double centerX = area.X + area.Width / 2;
            double centerY = area.Y + area.Height / 2;
            int halfRows = (int)Math.Floor(area.Height / 2 / rowStep) + 1;
            int halfColumns = (int)Math.Floor(area.Width / 2 / pitch) + 1;
            for (int r = -halfRows; r <= halfRows; r++)
            {
                double y = centerY + r * rowStep;
                double shift = staggered && Math.Abs(r) % 2 == 1 ? pitch / 2 : 0;
                for (int c = -halfColumns; c <= halfColumns; c++)
                {
                    double x = centerX + c * pitch + shift;
                    Point[] points = Shape(p, x, y, pitch);
                    if (Inside(points, area))
                        holes.Add(new Hole(points, new Point(x, y)));
                }
            }
            return holes;
        }

        private static double RowStep(PatternStyle p)
        {
            switch (p.Shape)
            {
                case HoleShape.Hexagon:
                case HoleShape.Circle:
                    return p.Pitch * Math.Sqrt(3) / 2;
                case HoleShape.Cross:
                    return p.Pitch / 2;
                case HoleShape.Slot:
                    return p.Pitch * p.Open * p.Aspect * 0.62 + p.Pitch * 0.12;
                case HoleShape.Diamond:
                    return p.Pitch * p.Aspect / 2;
                default:
                    return p.Pitch;
            }
        }

        private static Point[] Shape(PatternStyle p, double cx, double cy, double pitch)
        {
            double size = pitch * p.Open;
            switch (p.Shape)
            {
                case HoleShape.Hexagon:
                {
                    // Uchi tepaga qaragan olti burchak; yonlari orasidagi masofa = size
                    double r = size / Math.Sqrt(3);
                    var points = new Point[6];
                    for (int i = 0; i < 6; i++)
                    {
                        double angle = Math.PI / 2 + i * Math.PI / 3;
                        points[i] = new Point(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
                    }
                    return points;
                }

                case HoleShape.Circle:
                    return Ellipse(cx, cy, size / 2, size / 2, 16);

                case HoleShape.Cross:
                {
                    // Diagonal qo'shnilar bilan kesishmasligi uchun: a + w < pitch / 2
                    size = Math.Min(size, pitch * 0.7);
                    double a = size / 2;      // yelka uzunligi
                    double w = size / 6;      // yelka yarim eni
                    return new[]
                    {
                        new Point(cx - w, cy - a), new Point(cx + w, cy - a), new Point(cx + w, cy - w),
                        new Point(cx + a, cy - w), new Point(cx + a, cy + w), new Point(cx + w, cy + w),
                        new Point(cx + w, cy + a), new Point(cx - w, cy + a), new Point(cx - w, cy + w),
                        new Point(cx - a, cy + w), new Point(cx - a, cy - w), new Point(cx - w, cy - w),
                    };
                }

                case HoleShape.Square:
                {
                    double h = size / 2;
                    return new[] { new Point(cx - h, cy - h), new Point(cx + h, cy - h), new Point(cx + h, cy + h), new Point(cx - h, cy + h) };
                }

                case HoleShape.Slot:
                {
                    // Yonma-yon (siljigan) qatordagi tirqishlarga tegmasligi uchun eni qadamning yarmidan kam
                    size = Math.Min(size, pitch * 0.45);
                    double r = size / 2;
                    double half = Math.Max(size * p.Aspect / 2 - r, 0);
                    var points = new List<Point>();
                    for (int i = 0; i <= 6; i++)
                    {
                        double angle = Math.PI + i * Math.PI / 6; // yuqori yarim aylana
                        points.Add(new Point(cx + r * Math.Cos(angle), cy - half + r * Math.Sin(angle)));
                    }
                    for (int i = 0; i <= 6; i++)
                    {
                        double angle = i * Math.PI / 6; // pastki yarim aylana
                        points.Add(new Point(cx + r * Math.Cos(angle), cy + half + r * Math.Sin(angle)));
                    }
                    return points.ToArray();
                }

                default: // Diamond
                {
                    double hw = size / 2;
                    double hh = pitch * p.Aspect * p.Open / 2;
                    return new[] { new Point(cx, cy - hh), new Point(cx + hw, cy), new Point(cx, cy + hh), new Point(cx - hw, cy) };
                }
            }
        }

        private static Point[] Ellipse(double cx, double cy, double rx, double ry, int segments)
        {
            var points = new Point[segments];
            for (int i = 0; i < segments; i++)
            {
                double angle = 2 * Math.PI * i / segments;
                points[i] = new Point(cx + rx * Math.Cos(angle), cy + ry * Math.Sin(angle));
            }
            return points;
        }

        private static bool Inside(Point[] points, Rect area)
        {
            foreach (Point point in points)
            {
                if (point.X < area.Left || point.X > area.Right || point.Y < area.Top || point.Y > area.Bottom)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Nuqtadagi yorug'lik darajasi (0 — qorong'i, LightLevels-1 — eng yorug').
        /// background — fon to'rtburchagi, logo — logo to'rtburchagi (mm).
        /// </summary>
        public static int Level(BackgroundLight light, Point point, Rect background, Rect logo)
        {
            double value;
            switch (light)
            {
                case BackgroundLight.Full:
                    value = 1;
                    break;

                case BackgroundLight.Perimeter:
                {
                    double edge = Math.Min(Math.Min(point.X - background.Left, background.Right - point.X),
                                           Math.Min(point.Y - background.Top, background.Bottom - point.Y));
                    double reach = Math.Min(background.Width, background.Height) * 0.32;
                    value = 1 - edge / reach;
                    break;
                }

                case BackgroundLight.AroundLogo:
                {
                    double dx = Math.Max(Math.Max(logo.Left - point.X, point.X - logo.Right), 0);
                    double dy = Math.Max(Math.Max(logo.Top - point.Y, point.Y - logo.Bottom), 0);
                    double reach = Math.Max(logo.Height * 0.6, 100);
                    value = 1 - Math.Sqrt(dx * dx + dy * dy) / reach;
                    break;
                }

                case BackgroundLight.Bottom:
                    value = 1 - (background.Bottom - point.Y) / (background.Height * 0.75);
                    break;

                default:
                    value = 0;
                    break;
            }

            value = Num.Clamp(value, 0, 1);
            return (int)Math.Round(value * (LightLevels - 1));
        }

        /// <summary>Gorizontal/vertikal reykalar: har bir reyka to'rtburchagi (mm).</summary>
        public static List<Rect> Slats(Rect area, bool horizontal, out double gap)
        {
            double span = horizontal ? area.Height : area.Width;
            double slat = horizontal ? Num.Clamp(span * 0.06, 40, 120) : Num.Clamp(span * 0.035, 30, 80);
            gap = horizontal ? slat * 0.7 : slat * 0.45;
            int count = Math.Max(2, (int)Math.Floor((span + gap) / (slat + gap)));
            double start = (span - (count * slat + (count - 1) * gap)) / 2;

            var slats = new List<Rect>();
            for (int i = 0; i < count; i++)
            {
                double offset = start + i * (slat + gap);
                slats.Add(horizontal
                    ? new Rect(area.Left, area.Top + offset, area.Width, slat)
                    : new Rect(area.Left + offset, area.Top, slat, area.Height));
            }
            return slats;
        }

        /// <summary>Reykalar orasidagi tirqishlar (kechasi LED shu yerdan ko'rinadi), mayda bo'laklarga bo'lingan.</summary>
        public static List<Rect> SlatGaps(List<Rect> slats, bool horizontal, int pieces)
        {
            var gaps = new List<Rect>();
            for (int i = 0; i + 1 < slats.Count; i++)
            {
                Rect a = slats[i];
                Rect b = slats[i + 1];
                Rect gap = horizontal
                    ? new Rect(a.Left, a.Bottom, a.Width, Math.Max(b.Top - a.Bottom, 0.1))
                    : new Rect(a.Right, a.Top, Math.Max(b.Left - a.Right, 0.1), a.Height);

                for (int j = 0; j < pieces; j++)
                {
                    gaps.Add(horizontal
                        ? new Rect(gap.Left + gap.Width * j / pieces, gap.Top, gap.Width / pieces, gap.Height)
                        : new Rect(gap.Left, gap.Top + gap.Height * j / pieces, gap.Width, gap.Height / pieces));
                }
            }
            return gaps;
        }
    }
}
