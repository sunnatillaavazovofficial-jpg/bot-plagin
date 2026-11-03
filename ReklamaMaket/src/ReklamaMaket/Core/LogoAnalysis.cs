using System;
using System.Collections.Generic;
using System.Linq;

namespace ReklamaMaket.Core
{
    /// <summary>RGB rang (WPF'siz — tahlilni test qilish oson bo'lishi uchun).</summary>
    public struct Rgb24
    {
        public Rgb24(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public int DistanceTo(Rgb24 other)
        {
            int dr = R - other.R, dg = G - other.G, db = B - other.B;
            return (int)Math.Sqrt(dr * dr + dg * dg + db * db);
        }

        public override string ToString() => string.Format("#{0:X2}{1:X2}{2:X2}", R, G, B);
    }

    /// <summary>Piksel to'rtburchagi (chap-yuqoridan).</summary>
    public struct PixelRect
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    /// <summary>
    /// Logo rasmi (BGRA piksellar) tahlili: asosiy ranglar, orqadagi bir xil rangli katak (fon) va
    /// katak ichidagi logo chegarasi. Panel logoni olganda shu natija bilan o'lcham, rang va fonni to'ldiradi.
    /// </summary>
    public static class LogoAnalysis
    {
        private const int OpaqueAlpha = 200;
        private const int MergeDistance = 48;
        private const double MinShare = 0.06;
        private const int BackdropTolerance = 28;

        /// <summary>Asosiy ranglar, ko'pi birinchi. Juda kichik ulushdagilar (chet, soya) tashlanadi.</summary>
        public static List<Rgb24> DominantColors(byte[] bgra, int width, int height, int max = 4, Rgb24? ignore = null)
        {
            var buckets = new Dictionary<int, long[]>(); // kalit -> [soni, r, g, b]
            long total = 0;
            for (int i = 0; i + 3 < bgra.Length && i / 4 < width * height; i += 4)
            {
                if (bgra[i + 3] < OpaqueAlpha)
                    continue;
                var c = new Rgb24(bgra[i + 2], bgra[i + 1], bgra[i]);
                if (ignore.HasValue && c.DistanceTo(ignore.Value) <= BackdropTolerance)
                    continue;
                int key = (c.R >> 4) << 8 | (c.G >> 4) << 4 | (c.B >> 4);
                if (!buckets.TryGetValue(key, out long[] sum))
                    buckets[key] = sum = new long[4];
                sum[0]++;
                sum[1] += c.R;
                sum[2] += c.G;
                sum[3] += c.B;
                total++;
            }
            if (total == 0)
                return new List<Rgb24>();

            // O'xshash ranglarni birlashtirish (anti-aliasing va gradient bo'laklari)
            var clusters = new List<long[]>();
            foreach (long[] bucket in buckets.Values.OrderByDescending(b => b[0]))
            {
                Rgb24 color = Average(bucket);
                long[] near = clusters.FirstOrDefault(c => Average(c).DistanceTo(color) <= MergeDistance);
                if (near == null)
                {
                    clusters.Add((long[])bucket.Clone());
                }
                else
                {
                    for (int k = 0; k < 4; k++)
                        near[k] += bucket[k];
                }
            }

            return clusters.Where(c => c[0] >= total * MinShare)
                           .OrderByDescending(c => c[0])
                           .Take(max)
                           .Select(Average)
                           .ToList();
        }

        /// <summary>
        /// Rasm burchaklari bir xil rangli va to'liq bo'yalgan bo'lsa — bu orqadagi katak (fon) rangi.
        /// </summary>
        public static Rgb24? BackdropColor(byte[] bgra, int width, int height)
        {
            if (width < 8 || height < 8)
                return null;

            int dx = Math.Max(1, width * 3 / 100), dy = Math.Max(1, height * 3 / 100);
            var samples = new[]
            {
                Pixel(bgra, width, dx, dy), Pixel(bgra, width, width - 1 - dx, dy),
                Pixel(bgra, width, dx, height - 1 - dy), Pixel(bgra, width, width - 1 - dx, height - 1 - dy),
                Pixel(bgra, width, width / 2, dy), Pixel(bgra, width, width / 2, height - 1 - dy),
                Pixel(bgra, width, dx, height / 2), Pixel(bgra, width, width - 1 - dx, height / 2)
            };
            if (samples.Any(s => s == null))
                return null; // burchak shaffof — katak yo'q

            Rgb24 first = samples[0].Value;
            if (samples.Any(s => s.Value.DistanceTo(first) > BackdropTolerance))
                return null;

            return new Rgb24((byte)samples.Average(s => s.Value.R), (byte)samples.Average(s => s.Value.G), (byte)samples.Average(s => s.Value.B));
        }

        /// <summary>Katak ichida fon rangidan farq qiladigan piksellar chegarasi (logoning o'zi).</summary>
        public static PixelRect? ContentBounds(byte[] bgra, int width, int height, Rgb24 backdrop)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    if (bgra[i + 3] < OpaqueAlpha)
                        continue;
                    var c = new Rgb24(bgra[i + 2], bgra[i + 1], bgra[i]);
                    if (c.DistanceTo(backdrop) <= BackdropTolerance * 2)
                        continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0)
                return null;
            return new PixelRect { X = minX, Y = minY, Width = maxX - minX + 1, Height = maxY - minY + 1 };
        }

        /// <summary>Kesib olish va fon rangini shaffof qilish (chetlari yumshoq).</summary>
        public static byte[] CropAndKeyOut(byte[] bgra, int width, PixelRect rect, Rgb24 key)
        {
            var result = new byte[rect.Width * rect.Height * 4];
            for (int y = 0; y < rect.Height; y++)
            {
                for (int x = 0; x < rect.Width; x++)
                {
                    int s = ((rect.Y + y) * width + rect.X + x) * 4;
                    int d = (y * rect.Width + x) * 4;
                    var c = new Rgb24(bgra[s + 2], bgra[s + 1], bgra[s]);
                    int distance = c.DistanceTo(key);
                    double alpha = Math.Max(0, Math.Min(1, (distance - BackdropTolerance) / 40.0));
                    result[d] = bgra[s];
                    result[d + 1] = bgra[s + 1];
                    result[d + 2] = bgra[s + 2];
                    result[d + 3] = (byte)(bgra[s + 3] * alpha);
                }
            }
            return result;
        }

        private static Rgb24? Pixel(byte[] bgra, int width, int x, int y)
        {
            int i = (y * width + x) * 4;
            if (i + 3 >= bgra.Length || bgra[i + 3] < OpaqueAlpha)
                return null;
            return new Rgb24(bgra[i + 2], bgra[i + 1], bgra[i]);
        }

        private static Rgb24 Average(long[] sum) =>
            new Rgb24((byte)(sum[1] / sum[0]), (byte)(sum[2] / sum[0]), (byte)(sum[3] / sum[0]));
    }
}
