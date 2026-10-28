using System;
using System.Collections.Generic;

namespace ReklamaRasxod.Core
{
    /// <summary>Logoning haqiqiy o'lchamdagi geometriyasi (rasxod hisobi uchun).</summary>
    public sealed class LogoMeasures
    {
        /// <summary>Harflar yuzi (teshiklari ayirilgan), m².</summary>
        public double Area { get; set; }

        /// <summary>Barcha konturlar uzunligi (tashqi + ichki), m — bort, kant va kesish uzunligi.</summary>
        public double Perimeter { get; set; }

        /// <summary>Alohida bo'laklar (harflar) soni.</summary>
        public int Elements { get; set; }

        /// <summary>Har bir bo'lakning yuzi, m².</summary>
        public List<double> ElementAreas { get; } = new List<double>();

        /// <summary>O'rtacha chiziq (shtrix) qalinligi, mm: 2 × yuza / perimetr.</summary>
        public double StrokeWidth => Perimeter > 0 ? 2 * Area / Perimeter * 1000 : 0;
    }

    /// <summary>
    /// Logo shakli — shaffof fonli rasm (alfa kanal) asosida: yuza, kontur uzunligi, bo'laklar soni va
    /// harf ichiga sig'adigan nuqtalar (LED, lampochka) soni. Antialiasing hisobga olinadi, shuning uchun
    /// yuza ~1%, kontur ~2% aniqlikda chiqadi. Rasm logoning chegaralovchi to'rtburchagini to'liq egallaydi.
    /// </summary>
    public sealed class LogoShape
    {
        private const byte Threshold = 128;

        private readonly byte[] alpha;

        /// <param name="alpha">Har piksel uchun shaffofmaslik (0 — bo'sh, 255 — logo), qatorma-qator.</param>
        public LogoShape(byte[] alpha, int width, int height)
        {
            if (width <= 0 || height <= 0 || alpha == null || alpha.Length < width * height)
                throw new ArgumentException("Logo rasmi noto'g'ri.");

            this.alpha = alpha;
            Width = width;
            Height = height;
            Analyze();
        }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Yuza, piksel² (antialiasing bilan).</summary>
        public double AreaPixels { get; private set; }

        /// <summary>Kontur uzunligi, piksel.</summary>
        public double PerimeterPixels { get; private set; }

        /// <summary>Har bir bo'lakning yuzi, piksel² (kattadan kichikka).</summary>
        public List<int> Components { get; } = new List<int>();

        /// <summary>Logo chegaralovchi to'rtburchakning qancha qismini egallaydi (0..1).</summary>
        public double FillRatio => AreaPixels / ((double)Width * Height);

        /// <summary>Logo bo'sh (hech narsa chizilmagan).</summary>
        public bool IsEmpty => AreaPixels < 1;

        /// <summary>Haqiqiy o'lchamga o'tkazish: logo eni va bo'yi, mm.</summary>
        public LogoMeasures Measure(double widthMm, double heightMm)
        {
            double sx = widthMm / Width;  // mm / piksel
            double sy = heightMm / Height;
            double pixelArea = sx * sy / 1e6; // m²
            var result = new LogoMeasures
            {
                Area = AreaPixels * pixelArea,
                Perimeter = PerimeterPixels * Math.Sqrt(sx * sy) / 1000,
                Elements = Components.Count
            };
            foreach (int pixels in Components)
                result.ElementAreas.Add(pixels * pixelArea);
            return result;
        }

        /// <summary>
        /// Harf ichiga to'r bo'yicha joylashadigan nuqtalar soni (ochiq LED, lampochka, perforatsiya).
        /// Nuqta markazidan har tomonga <paramref name="clearanceMm"/> masofada ham harf bo'lishi kerak.
        /// </summary>
        public int CountInsidePoints(double widthMm, double heightMm, double spacingMm, double clearanceMm, bool staggered)
        {
            if (spacingMm <= 0)
                return 0;

            double sx = widthMm / Width;
            double sy = heightMm / Height;

            // Juda ko'p nuqta bo'lsa — siyrakroq to'r bilan sanab, natijani masshtablaymiz
            double scale = 1;
            while (widthMm / (spacingMm * scale) * (heightMm / (spacingMm * scale)) > 400000)
                scale *= 1.25;

            double step = spacingMm * scale;
            double rowStep = step * (staggered ? 0.866 : 1);
            int count = 0;
            int row = 0;
            for (double y = rowStep / 2; y < heightMm; y += rowStep, row++)
            {
                double shift = staggered && row % 2 == 1 ? step / 2 : 0;
                for (double x = step / 2 + shift; x < widthMm; x += step)
                {
                    if (Inside(x / sx, y / sy) &&
                        Inside((x - clearanceMm) / sx, y / sy) && Inside((x + clearanceMm) / sx, y / sy) &&
                        Inside(x / sx, (y - clearanceMm) / sy) && Inside(x / sx, (y + clearanceMm) / sy))
                    {
                        count++;
                    }
                }
            }
            return (int)Math.Round(count * scale * scale);
        }

        private bool Inside(double px, double py)
        {
            int x = (int)px;
            int y = (int)py;
            if (px < 0 || py < 0 || x >= Width || y >= Height)
                return false;
            return alpha[y * Width + x] >= Threshold;
        }

        private double A(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? 0 : alpha[y * Width + x] / 255.0;

        private void Analyze()
        {
            // Yuza — alfa yig'indisi; kontur — alfa gradienti yig'indisi (koarea formulasi)
            double area = 0;
            double perimeter = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    double a = alpha[y * Width + x] / 255.0;
                    area += a;
                    double gx = (A(x + 1, y) - A(x - 1, y)) / 2;
                    double gy = (A(x, y + 1) - A(x, y - 1)) / 2;
                    if (gx != 0 || gy != 0)
                        perimeter += Math.Sqrt(gx * gx + gy * gy);
                }
            }
            // Rasm cheti: logo chetga tegib turgan bo'lsa, tashqaridagi piksellar 0 deb olinadi
            for (int x = 0; x < Width; x++)
                perimeter += (A(x, 0) + A(x, Height - 1)) / 2;
            for (int y = 0; y < Height; y++)
                perimeter += (A(0, y) + A(Width - 1, y)) / 2;

            AreaPixels = area;
            PerimeterPixels = perimeter;
            FindComponents();
        }

        /// <summary>Alohida bo'laklar (8 tomonlama qo'shnichilik). Juda mayda dog'lar hisobga olinmaydi.</summary>
        private void FindComponents()
        {
            var seen = new bool[Width * Height];
            var stack = new Stack<int>();
            var sizes = new List<int>();
            long total = 0;

            for (int start = 0; start < seen.Length; start++)
            {
                if (seen[start] || alpha[start] < Threshold)
                    continue;

                int size = 0;
                seen[start] = true;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int index = stack.Pop();
                    size++;
                    int cx = index % Width;
                    int cy = index / Width;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = cy + dy;
                        if (ny < 0 || ny >= Height)
                            continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = cx + dx;
                            if (nx < 0 || nx >= Width)
                                continue;
                            int next = ny * Width + nx;
                            if (!seen[next] && alpha[next] >= Threshold)
                            {
                                seen[next] = true;
                                stack.Push(next);
                            }
                        }
                    }
                }
                sizes.Add(size);
                total += size;
            }

            int minimum = (int)Math.Max(6, total * 0.0003);
            sizes.RemoveAll(size => size < minimum);
            sizes.Sort((a, b) => b.CompareTo(a));
            Components.AddRange(sizes);
        }
    }
}
