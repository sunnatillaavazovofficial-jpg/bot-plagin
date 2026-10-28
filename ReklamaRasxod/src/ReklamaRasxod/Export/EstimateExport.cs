using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ReklamaMaket.Core;
using corel = Corel.Interop.VGCore;
using WpfColor = System.Windows.Media.Color;

namespace ReklamaRasxod.Export
{
    /// <summary>Smetaning bitta qatori.</summary>
    public sealed class SmetaRow
    {
        public string Group { get; set; }
        public string Name { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
        public double Price { get; set; }
        public string Note { get; set; }

        /// <summary>Qo'lda qo'shilgan qator (ish haqi, montaj...).</summary>
        public bool Custom { get; set; }

        public double Sum => Quantity * Price;
    }

    /// <summary>Tayyor smeta: dizayn haqida ma'lumot, qatorlar va jami.</summary>
    public sealed class Smeta
    {
        public DateTime Created { get; set; }
        public string Currency { get; set; } = "so'm";
        public double MarkupPercent { get; set; }
        public List<string> Info { get; } = new List<string>();
        public List<SmetaRow> Rows { get; } = new List<SmetaRow>();

        public double Materials => Rows.Where(row => !row.Custom).Sum(row => row.Sum);
        public double Extra => Rows.Where(row => row.Custom).Sum(row => row.Sum);
        public double Markup => (Materials + Extra) * MarkupPercent / 100;
        public double Total => Materials + Extra + Markup;
        public int MissingPrices => Rows.Count(row => !row.Custom && row.Quantity > 0 && row.Price <= 0);
    }

    /// <summary>Pul summasi: 1 250 000 (minglar bo'sh joy bilan, butun songacha).</summary>
    public static class MoneyText
    {
        private static readonly NumberFormatInfo Culture = new NumberFormatInfo { NumberGroupSeparator = " ", NumberDecimalSeparator = "," };

        public static string Format(double value) => Math.Round(value).ToString("#,0", Culture);
    }

    /// <summary>Smetani Excel (CSV) fayliga yozish va Corel sahifasiga jadval qilib qo'yish.</summary>
    public static class EstimateExport
    {
        private const double PointsPerMm = 72 / 25.4;

        private static readonly WpfColor Ink = ColorMath.Rgb(0x222222);
        private static readonly WpfColor Muted = ColorMath.Rgb(0x69717C);
        private static readonly WpfColor Accent = ColorMath.Rgb(0x11683F);
        private static readonly WpfColor HeaderFill = ColorMath.Rgb(0xE8F5EE);
        private static readonly WpfColor GridLine = ColorMath.Rgb(0xC9CED3);

        // ================================================================ CSV

        /// <summary>Excel uchun CSV: UTF-8 (BOM bilan), ajratuvchi ";", kasr — vergul.</summary>
        public static void WriteCsv(string file, Smeta smeta)
        {
            var csv = new StringBuilder();
            Line(csv, "Reklama Rasxod — material rasxodi va smeta");
            Line(csv, "Sana", smeta.Created.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture));
            foreach (string info in smeta.Info)
                Line(csv, info);
            csv.AppendLine();

            Line(csv, "№", "Material / ish", "Miqdor", "Birlik", "Narx (" + smeta.Currency + ")", "Summa (" + smeta.Currency + ")", "Qayerga");
            int number = 0;
            foreach (IGrouping<string, SmetaRow> group in smeta.Rows.GroupBy(row => row.Group))
            {
                Line(csv, string.Empty, group.Key);
                foreach (SmetaRow row in group)
                {
                    number++;
                    Line(csv, number.ToString(CultureInfo.InvariantCulture), row.Name, Decimal(row.Quantity), row.Unit,
                        row.Price > 0 ? Decimal(row.Price) : string.Empty, row.Price > 0 ? Decimal(Math.Round(row.Sum)) : string.Empty, row.Note);
                }
            }
            csv.AppendLine();
            Line(csv, string.Empty, "Materiallar", string.Empty, string.Empty, string.Empty, Decimal(Math.Round(smeta.Materials)));
            Line(csv, string.Empty, "Qo'shimcha xarajatlar", string.Empty, string.Empty, string.Empty, Decimal(Math.Round(smeta.Extra)));
            if (smeta.MarkupPercent > 0)
                Line(csv, string.Empty, "Ustama " + Decimal(smeta.MarkupPercent) + "%", string.Empty, string.Empty, string.Empty, Decimal(Math.Round(smeta.Markup)));
            Line(csv, string.Empty, "JAMI", string.Empty, string.Empty, string.Empty, Decimal(Math.Round(smeta.Total)));
            if (smeta.MissingPrices > 0)
                Line(csv, string.Empty, smeta.MissingPrices + " ta materialga narx kiritilmagan");

            File.WriteAllText(file, csv.ToString(), new UTF8Encoding(true));
        }

        private static void Line(StringBuilder csv, params string[] cells)
        {
            csv.AppendLine(string.Join(";", cells.Select(Escape)));
        }

        private static string Escape(string cell)
        {
            cell = cell ?? string.Empty;
            if (cell.IndexOfAny(new[] { ';', '"', '\n', '\r' }) < 0)
                return cell;
            return "\"" + cell.Replace("\"", "\"\"") + "\"";
        }

        private static string Decimal(double value) => value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

        // ================================================================ Corel

        /// <summary>
        /// Smetani jadval qilib Corel sahifasiga qo'yadi: logoning o'ng tomoniga (logo bo'lmasa — sahifa o'ng tomoniga).
        /// Hammasi bitta guruh, bitta Ctrl+Z bilan bekor qilinadi.
        /// </summary>
        public static corel.Shape DrawInCorel(corel.Application app, corel.Document doc, Smeta smeta, corel.ShapeRange near)
        {
            if (app == null)
                throw new UserException("CorelDRAW bilan bog'lanib bo'lmadi.");

            const double text = 4;          // yozuv balandligi, mm
            const double row = text * 1.75; // qator balandligi
            const double pad = text * 0.6;
            const double charWidth = text * 0.56;

            corel.cdrUnit oldUnit = doc.Unit;
            doc.BeginCommandGroup("Rasxod smeta");
            corel.ShapeRange created = app.CreateShapeRange();
            try
            {
                app.Optimization = true;
                app.EventsEnabled = false;
                doc.Unit = corel.cdrUnit.cdrMillimeter;
                corel.Layer layer = doc.ActiveLayer;

                double left, top;
                try
                {
                    if (near == null)
                        throw new InvalidOperationException();
                    left = near.RightX + 30;
                    top = near.BottomY + near.SizeHeight;
                }
                catch
                {
                    corel.Page page = doc.ActivePage;
                    left = page.CenterX + page.SizeWidth / 2 + 30;
                    top = page.CenterY + page.SizeHeight / 2;
                }

                // Ustunlar: №, nomi, miqdor, narx, summa
                var money = smeta.Rows.Select(r => r.Price > 0 ? MoneyText.Format(r.Price) : "—").ToList();
                var sums = smeta.Rows.Select(r => r.Price > 0 ? MoneyText.Format(r.Sum) : "—").ToList();
                double wNumber = charWidth * 3 + pad;
                double wName = Num.Clamp(smeta.Rows.Select(r => r.Name.Length).DefaultIfEmpty(10).Max() * charWidth + pad, 60, 170);
                double wQuantity = Math.Max(smeta.Rows.Select(r => (Num.Format(r.Quantity) + " " + r.Unit).Length).DefaultIfEmpty(6).Max(), 6) * charWidth + pad;
                double wPrice = Math.Max(money.Select(m => m.Length).DefaultIfEmpty(6).Max(), 6) * charWidth + pad;
                double wSum = Math.Max(Math.Max(sums.Select(m => m.Length).DefaultIfEmpty(6).Max(), MoneyText.Format(smeta.Total).Length + 2), 8) * charWidth * 1.15 + pad;
                double width = wNumber + wName + wQuantity + wPrice + wSum;
                double[] x = { left, left + wNumber, left + wNumber + wName, left + wNumber + wName + wQuantity, left + width - wSum, left + width };

                double y = top;
                created.Add(Text(layer, "SMETA — material rasxodi", left, y - text * 1.6 / 2, text * 1.6, Accent, false));
                y -= text * 2.4;
                created.Add(Text(layer, "Sana: " + smeta.Created.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), left, y - text * 0.4, text * 0.85, Muted, false));
                y -= text * 1.3;
                foreach (string info in smeta.Info)
                {
                    created.Add(Text(layer, info, left, y - text * 0.4, text * 0.85, Muted, false));
                    y -= text * 1.3;
                }
                y -= text * 0.6;

                // Sarlavha qatori
                created.Add(Box(layer, left, y - row, width, row, HeaderFill));
                string[] headers = { "№", "Material / ish", "Miqdor", "Narx, " + smeta.Currency, "Summa, " + smeta.Currency };
                for (int c = 0; c < headers.Length; c++)
                {
                    bool right = c >= 2;
                    created.Add(Text(layer, headers[c], right ? x[c + 1] - pad / 2 : x[c] + pad / 2, y - row / 2, text * 0.9, Accent, right));
                }
                y -= row;

                int number = 0;
                foreach (IGrouping<string, SmetaRow> group in smeta.Rows.GroupBy(r => r.Group))
                {
                    created.Add(Text(layer, group.Key, x[1] + pad / 2, y - row / 2, text * 0.8, Accent, false));
                    y -= row;
                    foreach (SmetaRow r in group)
                    {
                        number++;
                        int i = smeta.Rows.IndexOf(r);
                        created.Add(Text(layer, number.ToString(CultureInfo.InvariantCulture), x[0] + pad / 2, y - row / 2, text, Ink, false));
                        created.Add(Text(layer, r.Name, x[1] + pad / 2, y - row / 2, text, Ink, false));
                        created.Add(Text(layer, Num.Format(r.Quantity) + " " + r.Unit, x[3] - pad / 2, y - row / 2, text, Ink, true));
                        created.Add(Text(layer, money[i], x[4] - pad / 2, y - row / 2, text, Ink, true));
                        created.Add(Text(layer, sums[i], x[5] - pad / 2, y - row / 2, text, Ink, true));
                        created.Add(Line(layer, left, y - row, left + width, y - row, GridLine));
                        y -= row;
                    }
                }

                y -= row * 0.4;
                var totals = new List<KeyValuePair<string, double>>
                {
                    new KeyValuePair<string, double>("Materiallar", smeta.Materials),
                    new KeyValuePair<string, double>("Qo'shimcha xarajatlar", smeta.Extra),
                };
                if (smeta.MarkupPercent > 0)
                    totals.Add(new KeyValuePair<string, double>("Ustama " + Num.Format(smeta.MarkupPercent) + "%", smeta.Markup));
                foreach (KeyValuePair<string, double> total in totals)
                {
                    created.Add(Text(layer, total.Key, x[1] + pad / 2, y - row / 2, text, Ink, false));
                    created.Add(Text(layer, MoneyText.Format(total.Value), x[5] - pad / 2, y - row / 2, text, Ink, true));
                    y -= row;
                }
                created.Add(Box(layer, left, y - row * 1.2, width, row * 1.2, HeaderFill));
                created.Add(Text(layer, "JAMI", x[1] + pad / 2, y - row * 0.6, text * 1.25, Accent, false));
                created.Add(Text(layer, MoneyText.Format(smeta.Total) + " " + smeta.Currency, x[5] - pad / 2, y - row * 0.6, text * 1.25, Accent, true));
                y -= row * 1.2;
                if (smeta.MissingPrices > 0)
                    created.Add(Text(layer, smeta.MissingPrices + " ta materialga narx kiritilmagan", left, y - row / 2, text * 0.85, Muted, false));

                corel.Shape table = created.Group();
                table.Name = "Rasxod smeta";
                return table;
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
                app.EventsEnabled = true;
                app.Optimization = false;
                doc.EndCommandGroup();
                app.Refresh();
            }
        }

        /// <summary>Yozuv: chap (yoki o'ng) cheti x da, o'rtasi y da.</summary>
        private static corel.Shape Text(corel.Layer layer, string value, double x, double centerY, double heightMm, WpfColor color, bool alignRight)
        {
            corel.Shape shape = layer.CreateArtisticText(x, centerY, string.IsNullOrEmpty(value) ? " " : value);
            shape.Text.Story.Font = "Arial";
            shape.Text.Story.Size = (float)(heightMm * PointsPerMm);
            shape.Fill.UniformColor.RGBAssign(color.R, color.G, color.B);
            shape.CenterX = alignRight ? x - shape.SizeWidth / 2 : x + shape.SizeWidth / 2;
            shape.CenterY = centerY;
            return shape;
        }

        private static corel.Shape Box(corel.Layer layer, double x, double y, double width, double height, WpfColor color)
        {
            corel.Shape shape = layer.CreateRectangle2(x, y, width, height);
            shape.Fill.UniformColor.RGBAssign(color.R, color.G, color.B);
            shape.Outline.SetNoOutline();
            return shape;
        }

        private static corel.Shape Line(corel.Layer layer, double x1, double y1, double x2, double y2, WpfColor color)
        {
            corel.Shape line = layer.CreateLineSegment(x1, y1, x2, y2);
            line.Outline.Width = 0.15;
            line.Outline.Color.RGBAssign(color.R, color.G, color.B);
            return line;
        }
    }
}
