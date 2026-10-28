using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReklamaMaket.Core;
using ReklamaRasxod.Core;
using ReklamaRasxod.Estimate;
using ReklamaRasxod.Export;

namespace ReklamaRasxod
{
    /// <summary>
    /// Rasxod jadvali: hisoblangan materiallar (miqdorini qo'lda o'zgartirish mumkin), narxlar (saqlanadi),
    /// qo'shimcha qatorlar, ustama, jami summa, konstruksiya tanlovlari va sarf normalari; Excel va Corelga eksport.
    /// </summary>
    public partial class DockerUI
    {
        /// <summary>Qo'lda o'zgartirilgan miqdorlar (kalit — material + birlik). Dizayn turi o'zgarsa tozalanadi.</summary>
        private readonly Dictionary<string, double> quantityOverrides = new Dictionary<string, double>();
        private readonly List<MaterialRow> materialRows = new List<MaterialRow>();
        private string materialSignature;
        private EstimateResult lastEstimate;
        private SignSettings lastSettings;

        private sealed class MaterialRow
        {
            public MaterialLine Line;
            public TextBlock Name;
            public TextBox Quantity;
            public TextBlock Unit;
            public TextBox Price;
            public TextBlock Sum;
            public TextBlock Extra;
            public TextBlock Note;
            public Button Reset;
        }

        private string Currency => string.IsNullOrWhiteSpace(store.Currency) ? "so'm" : store.Currency.Trim();

        // ================================================================ boshlang'ich sozlash

        private void SetupEstimate()
        {
            ReturnsCombo.ItemsSource = new[]
            {
                new Choice<ReturnKind>("Avto (dizayn bo'yicha)", ReturnKind.Auto),
                new Choice<ReturnKind>("Alyuminiy bort lentasi", ReturnKind.AluminiumTape),
                new Choice<ReturnKind>("Fomaks tasma", ReturnKind.FomaxStrip),
                new Choice<ReturnKind>("Sutsimon akril tasma (yonadi)", ReturnKind.AcrylicStrip),
                new Choice<ReturnKind>("Alyukobond tasma", ReturnKind.AcpStrip),
                new Choice<ReturnKind>("Metall list", ReturnKind.MetalSheet),
            };
            BackCombo.ItemsSource = new[]
            {
                new Choice<BackKind>("Avto (dizayn bo'yicha)", BackKind.Auto),
                new Choice<BackKind>("Fomaks (PVX)", BackKind.Fomax),
                new Choice<BackKind>("Alyukobond", BackKind.Acp),
                new Choice<BackKind>("Shaffof akril", BackKind.ClearAcrylic),
                new Choice<BackKind>("Metall list", BackKind.Metal),
                new Choice<BackKind>("Orqasi ochiq", BackKind.None),
            };
            FomaxCombo.ItemsSource = new[] { 3.0, 4.0, 5.0, 6.0, 8.0, 10.0 };
            AcpCombo.ItemsSource = new[] { 3.0, 4.0 };
            ProfileCombo.ItemsSource = new[] { "15x15", "20x20", "20x40", "25x25", "30x30", "40x20", "40x40", "60x40" };
            SheetUnitCombo.ItemsSource = new[]
            {
                new Choice<bool>("m² bo'yicha (list soni izohda)", false),
                new Choice<bool>("Butun list bo'yicha (narx — 1 list)", true),
            };

            ConstructionChoice choice = store.ToChoice();
            ReturnsCombo.SelectedItem = ReturnsCombo.Items.Cast<Choice<ReturnKind>>().First(c => c.Value == choice.Returns);
            BackCombo.SelectedItem = BackCombo.Items.Cast<Choice<BackKind>>().First(c => c.Value == choice.Back);
            FomaxCombo.SelectedItem = FomaxCombo.Items.Cast<double>().Contains(choice.FomaxThickness) ? choice.FomaxThickness : 5.0;
            AcpCombo.SelectedItem = choice.AcpThickness <= 3 ? 3.0 : 4.0;
            string profile = choice.ProfileA + "x" + choice.ProfileB;
            ProfileCombo.SelectedItem = ProfileCombo.Items.Cast<string>().Contains(profile) ? profile : "20x20";
            SheetUnitCombo.SelectedIndex = store.WholeSheets ? 1 : 0;
            PaintReturnsCheck.IsChecked = store.PaintReturns;

            MarkupBox.Text = Num.Format(store.Markup);
            CurrencyBox.Text = Currency;
            BuildNormRows();
            BuildCustomRows();
        }

        private void Construction_Changed(object sender, RoutedEventArgs e)
        {
            if (suppressEvents)
                return;

            store.Returns = (ReturnsCombo.SelectedItem as Choice<ReturnKind>)?.Value.ToString();
            store.Back = (BackCombo.SelectedItem as Choice<BackKind>)?.Value.ToString();
            if (FomaxCombo.SelectedItem is double fomax)
                store.FomaxThickness = fomax;
            if (AcpCombo.SelectedItem is double acp)
                store.AcpThickness = acp;
            if (ProfileCombo.SelectedItem is string profile)
                store.Profile = profile;
            store.WholeSheets = (SheetUnitCombo.SelectedItem as Choice<bool>)?.Value == true;
            store.PaintReturns = PaintReturnsCheck.IsChecked == true;
            ScheduleSave();
            ScheduleRefresh();
        }

        // ================================================================ hisob

        private void Recalculate(SignSettings settings)
        {
            lastSettings = settings;
            var norms = new Norms();
            foreach (KeyValuePair<string, double> pair in store.Norms)
                norms.Values[pair.Key] = pair.Value;

            LogoCapture source = logo ?? LogoCapture.Placeholder();
            EstimateResult result = Estimator.Compute(new EstimateInput
            {
                Sign = settings,
                Logo = source.Shape,
                Choice = store.ToChoice(),
                Norms = norms
            });
            if (logo == null)
                result.Notes.Insert(0, "Logo tanlanmagan — hisob «LOGO» namunasi bo'yicha (taxminiy). Corelda logoni belgilab, «Tanlangan logoni olish»ni bosing.");
            lastEstimate = result;

            LogoMeasures m = result.Measures;
            MeasureText.Text = string.Format("O'lchov: harflar yuzi {0} m² · kontur (bort) {1} m · {2} ta bo'lak · o'rtacha chiziq qalinligi {3} mm",
                Num.Format(Math.Round(m.Area, 3)), Num.Format(Math.Round(m.Perimeter, 2)), m.Elements, Num.Format(Math.Round(m.StrokeWidth)));

            var construction = result.Construction.Select(line => "• " + line).ToList();
            if (result.Watts12 > 0 || result.Watts220 > 0)
            {
                construction.Add("• Quvvat: " + (result.Watts12 > 0 ? "12V — " + Num.Format(Math.Round(result.Watts12, 1)) + " W" : string.Empty) +
                                 (result.Watts12 > 0 && result.Watts220 > 0 ? ", " : string.Empty) +
                                 (result.Watts220 > 0 ? "220V — " + Num.Format(Math.Round(result.Watts220, 1)) + " W" : string.Empty));
            }
            ConstructionText.Text = string.Join("\n", construction);

            NotesText.Text = string.Join("\n", result.Notes.Select(note => "• " + note));
            NotesText.Visibility = result.Notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            string signature = string.Join(",", result.Lines.Select(line => line.PriceKey));
            if (signature != materialSignature)
            {
                materialSignature = signature;
                BuildMaterialRows(result);
            }
            else
            {
                for (int i = 0; i < materialRows.Count; i++)
                    materialRows[i].Line = result.Lines[i];
            }
            UpdateMaterialRows();
            UpdateTotals();
        }

        private void ClearOverrides()
        {
            quantityOverrides.Clear();
            materialSignature = null;
        }

        private double QuantityOf(MaterialLine line) =>
            quantityOverrides.TryGetValue(line.PriceKey, out double quantity) ? quantity : line.Quantity;

        private double PriceOf(string key) => store.Prices.TryGetValue(key, out double price) ? price : 0;

        // ================================================================ materiallar jadvali

        private void BuildMaterialRows(EstimateResult result)
        {
            MaterialsPanel.Children.Clear();
            materialRows.Clear();

            if (result.Lines.Count == 0)
            {
                MaterialsPanel.Children.Add(new TextBlock { Style = (Style)FindResource("Hint"), Text = "Hisoblanadigan material yo'q." });
                return;
            }

            foreach (IGrouping<MaterialGroup, MaterialLine> group in result.Lines.GroupBy(line => line.Group))
            {
                MaterialsPanel.Children.Add(new TextBlock { Style = (Style)FindResource("GroupTitle"), Text = MaterialGroups.Title(group.Key) });
                foreach (MaterialLine line in group)
                    MaterialsPanel.Children.Add(CreateMaterialRow(line));
            }
        }

        private UIElement CreateMaterialRow(MaterialLine line)
        {
            var row = new MaterialRow { Line = line };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int i = 0; i < 4; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            row.Name = new TextBlock { Text = line.Name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("TextBrush") };
            grid.Children.Add(row.Name);

            row.Sum = new TextBlock { FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(row.Sum, 1);
            grid.Children.Add(row.Sum);

            // Miqdor × narx
            var inputs = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Quantity = new TextBox { Tag = row, ToolTip = "Miqdor. O'zgartirsangiz — qo'lda kiritilgan qiymat ishlatiladi." };
            row.Quantity.TextChanged += MaterialQuantity_TextChanged;
            inputs.Children.Add(row.Quantity);

            row.Unit = new TextBlock { Style = (Style)FindResource("Hint"), Margin = new Thickness(4, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(row.Unit, 1);
            inputs.Children.Add(row.Unit);

            row.Price = new TextBox { Tag = row };
            row.Price.TextChanged += MaterialPrice_TextChanged;
            row.Price.LostFocus += (sender, e) => FormatPriceBox(row.Price, PriceOf(row.Line.PriceKey));
            Grid.SetColumn(row.Price, 2);
            inputs.Children.Add(row.Price);

            var currency = new TextBlock { Style = (Style)FindResource("Hint"), Text = Currency, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(currency, 3);
            inputs.Children.Add(currency);

            row.Reset = new Button
            {
                Style = (Style)FindResource("SecondaryButton"),
                Content = "↺",
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "Hisoblangan miqdorga qaytarish",
                Tag = row
            };
            row.Reset.Click += MaterialReset_Click;
            Grid.SetColumn(row.Reset, 4);
            inputs.Children.Add(row.Reset);

            Grid.SetRow(inputs, 1);
            Grid.SetColumnSpan(inputs, 2);
            grid.Children.Add(inputs);

            row.Extra = new TextBlock { Style = (Style)FindResource("Hint"), Margin = new Thickness(0, 2, 0, 0) };
            Grid.SetRow(row.Extra, 2);
            Grid.SetColumnSpan(row.Extra, 2);
            grid.Children.Add(row.Extra);

            row.Note = new TextBlock { Style = (Style)FindResource("Hint"), FontSize = 10.5, FontStyle = FontStyles.Italic };
            Grid.SetRow(row.Note, 3);
            Grid.SetColumnSpan(row.Note, 2);
            grid.Children.Add(row.Note);

            FormatPriceBox(row.Price, PriceOf(line.PriceKey));
            materialRows.Add(row);
            return new Border { Style = (Style)FindResource("MaterialRow"), Child = grid };
        }

        private void UpdateMaterialRows()
        {
            bool previous = suppressEvents;
            suppressEvents = true;
            try
            {
                foreach (MaterialRow row in materialRows)
                {
                    MaterialLine line = row.Line;
                    bool overridden = quantityOverrides.ContainsKey(line.PriceKey);
                    if (!overridden && !row.Quantity.IsKeyboardFocusWithin)
                        row.Quantity.Text = Num.Format(line.Quantity);
                    row.Quantity.Foreground = (Brush)FindResource(overridden ? "AccentDarkBrush" : "TextBrush");
                    row.Quantity.FontWeight = overridden ? FontWeights.Bold : FontWeights.Normal;
                    row.Reset.Visibility = overridden ? Visibility.Visible : Visibility.Hidden;
                    row.Name.Text = line.Name;
                    row.Unit.Text = line.Unit + " ×";
                    row.Extra.Text = overridden ? "Hisob bo'yicha: " + Num.Format(line.Quantity) + " " + line.Unit + (string.IsNullOrEmpty(line.Extra) ? string.Empty : " · " + line.Extra) : line.Extra;
                    row.Extra.Visibility = string.IsNullOrEmpty(row.Extra.Text) ? Visibility.Collapsed : Visibility.Visible;
                    row.Note.Text = line.Note;
                    row.Note.Visibility = string.IsNullOrEmpty(line.Note) ? Visibility.Collapsed : Visibility.Visible;
                    UpdateRowSum(row);
                }
            }
            finally
            {
                suppressEvents = previous;
            }
        }

        private void UpdateRowSum(MaterialRow row)
        {
            double price = PriceOf(row.Line.PriceKey);
            double quantity = QuantityOf(row.Line);
            if (price > 0)
            {
                row.Sum.Text = MoneyText.Format(quantity * price);
                row.Sum.Foreground = (Brush)FindResource("TextBrush");
            }
            else
            {
                row.Sum.Text = quantity > 0 ? "narx yo'q" : "—";
                row.Sum.Foreground = (Brush)FindResource("MutedBrush");
            }
        }

        private void FormatPriceBox(TextBox box, double price)
        {
            bool previous = suppressEvents;
            suppressEvents = true;
            box.Text = price > 0 ? MoneyText.Format(price) : string.Empty;
            box.ClearValue(BorderBrushProperty);
            suppressEvents = previous;
        }

        private void MaterialQuantity_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents)
                return;

            var box = (TextBox)sender;
            var row = (MaterialRow)box.Tag;
            string text = box.Text.Trim();
            if (text.Length == 0)
            {
                quantityOverrides.Remove(row.Line.PriceKey);
            }
            else if (Num.TryParse(text, out double value) && value >= 0 && value < 1e7)
            {
                if (Math.Abs(value - row.Line.Quantity) < 1e-9)
                    quantityOverrides.Remove(row.Line.PriceKey);
                else
                    quantityOverrides[row.Line.PriceKey] = value;
            }
            else
            {
                box.BorderBrush = (Brush)FindResource("ErrorBrush");
                return;
            }
            box.ClearValue(BorderBrushProperty);
            UpdateMaterialRows();
            UpdateTotals();
        }

        private void MaterialPrice_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents)
                return;

            var box = (TextBox)sender;
            var row = (MaterialRow)box.Tag;
            string text = box.Text.Trim();
            if (text.Length == 0)
            {
                store.Prices.Remove(row.Line.PriceKey);
            }
            else if (Num.TryParse(text, out double value) && value >= 0 && value < 1e12)
            {
                store.Prices[row.Line.PriceKey] = value;
            }
            else
            {
                box.BorderBrush = (Brush)FindResource("ErrorBrush");
                return;
            }
            box.ClearValue(BorderBrushProperty);
            UpdateRowSum(row);
            UpdateTotals();
            ScheduleSave();
        }

        private void MaterialReset_Click(object sender, RoutedEventArgs e)
        {
            var row = (MaterialRow)((FrameworkElement)sender).Tag;
            quantityOverrides.Remove(row.Line.PriceKey);
            UpdateMaterialRows();
            UpdateTotals();
        }

        private void ResetQuantitiesButton_Click(object sender, RoutedEventArgs e)
        {
            quantityOverrides.Clear();
            UpdateMaterialRows();
            UpdateTotals();
        }

        // ================================================================ qo'shimcha qatorlar

        private void BuildCustomRows()
        {
            CustomPanel.Children.Clear();
            foreach (CustomRow row in store.CustomRows)
                CustomPanel.Children.Add(CreateCustomRow(row));
        }

        private UIElement CreateCustomRow(CustomRow row)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var name = new TextBox { Text = row.Name, HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = "Nomi (ish haqi, montaj, transport, dizayn...)" };
            grid.Children.Add(name);

            var delete = new Button { Style = (Style)FindResource("SecondaryButton"), Content = "×", Padding = new Thickness(7, 1, 7, 1), Margin = new Thickness(4, 0, 0, 0), ToolTip = "Qatorni o'chirish" };
            Grid.SetColumn(delete, 1);
            grid.Children.Add(delete);

            var inputs = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputs.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var quantity = new TextBox { Text = Num.Format(row.Quantity), ToolTip = "Miqdor" };
            inputs.Children.Add(quantity);
            var unitBox = new TextBox { Text = row.Unit, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(4, 2, 0, 2), ToolTip = "Birlik" };
            Grid.SetColumn(unitBox, 1);
            inputs.Children.Add(unitBox);
            var times = new TextBlock { Style = (Style)FindResource("Hint"), Text = "×", Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(times, 2);
            inputs.Children.Add(times);
            var price = new TextBox { Text = row.Price > 0 ? MoneyText.Format(row.Price) : string.Empty, ToolTip = "Narxi (" + Currency + ")" };
            Grid.SetColumn(price, 3);
            inputs.Children.Add(price);
            var sum = new TextBlock { FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(sum, 4);
            inputs.Children.Add(sum);
            Grid.SetRow(inputs, 1);
            Grid.SetColumnSpan(inputs, 2);
            grid.Children.Add(inputs);

            void UpdateSum() => sum.Text = row.Sum > 0 ? MoneyText.Format(row.Sum) : "—";
            UpdateSum();

            name.TextChanged += (sender, e) =>
            {
                row.Name = name.Text;
                ScheduleSave();
            };
            unitBox.TextChanged += (sender, e) =>
            {
                row.Unit = unitBox.Text;
                ScheduleSave();
            };
            quantity.TextChanged += (sender, e) =>
            {
                if (TryReadAmount(quantity, out double value))
                {
                    row.Quantity = value;
                    UpdateSum();
                    UpdateTotals();
                    ScheduleSave();
                }
            };
            price.TextChanged += (sender, e) =>
            {
                if (TryReadAmount(price, out double value))
                {
                    row.Price = value;
                    UpdateSum();
                    UpdateTotals();
                    ScheduleSave();
                }
            };
            price.LostFocus += (sender, e) => FormatPriceBox(price, row.Price);
            delete.Click += (sender, e) =>
            {
                store.CustomRows.Remove(row);
                BuildCustomRows();
                UpdateTotals();
                ScheduleSave();
            };

            return new Border { Style = (Style)FindResource("MaterialRow"), Child = grid };
        }

        /// <summary>Bo'sh — 0; noto'g'ri son — maydon qizil bo'ladi.</summary>
        private bool TryReadAmount(TextBox box, out double value)
        {
            value = 0;
            string text = box.Text.Trim();
            if (text.Length == 0 || (Num.TryParse(text, out value) && value >= 0 && value < 1e12))
            {
                box.ClearValue(BorderBrushProperty);
                return true;
            }
            box.BorderBrush = (Brush)FindResource("ErrorBrush");
            return false;
        }

        private void AddRowButton_Click(object sender, RoutedEventArgs e)
        {
            store.CustomRows.Add(new CustomRow { Name = "Qo'shimcha", Unit = "dona" });
            BuildCustomRows();
            UpdateTotals();
            ScheduleSave();
        }

        private void MarkupBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents)
                return;
            if (TryReadAmount(MarkupBox, out double value))
            {
                store.Markup = Math.Min(value, 1000);
                UpdateTotals();
                ScheduleSave();
            }
        }

        private void CurrencyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents)
                return;
            store.Currency = CurrencyBox.Text.Trim();
            materialSignature = null; // qatorlardagi valyuta yozuvi yangilansin
            BuildCustomRows();
            ScheduleSave();
            ScheduleRefresh();
        }

        // ================================================================ jami

        private void UpdateTotals()
        {
            Smeta smeta = BuildSmeta();
            GrandTotalText.Text = MoneyText.Format(smeta.Total) + " " + Currency;
            string text = "Materiallar: " + MoneyText.Format(smeta.Materials) + " · qo'shimcha: " + MoneyText.Format(smeta.Extra);
            if (smeta.MarkupPercent > 0)
                text += " · ustama " + Num.Format(smeta.MarkupPercent) + "%: " + MoneyText.Format(smeta.Markup);
            if (smeta.MissingPrices > 0)
                text += "\n" + smeta.MissingPrices + " ta materialga narx kiritilmagan — jamiga qo'shilmadi.";
            TotalsBreakdownText.Text = text;
        }

        /// <summary>Paneldagi holat bo'yicha smeta (eksport va jami uchun).</summary>
        private Smeta BuildSmeta()
        {
            var smeta = new Smeta { Currency = Currency, MarkupPercent = store.Markup, Created = DateTime.Now };
            if (lastSettings != null)
            {
                smeta.Info.Add("Logo dizayni: " + lastSettings.Letter.Name);
                smeta.Info.Add("Fon: " + lastSettings.Background.Name);
                SignLayout layout = LogoLayout.Compute(lastSettings);
                smeta.Info.Add(string.Format("Logo: {0} × {1} mm, qalinligi {2} mm; fon: {3} × {4} mm",
                    Num.Format(Math.Round(lastSettings.LogoWidth)), Num.Format(Math.Round(lastSettings.LogoHeight)), Num.Format(lastSettings.Depth),
                    Num.Format(Math.Round(layout.BackgroundWidth)), Num.Format(Math.Round(layout.BackgroundHeight))));
            }
            if (lastEstimate != null)
            {
                LogoMeasures m = lastEstimate.Measures;
                smeta.Info.Add(string.Format("Harflar yuzi {0} m², kontur {1} m, {2} ta bo'lak",
                    Num.Format(Math.Round(m.Area, 3)), Num.Format(Math.Round(m.Perimeter, 2)), m.Elements));
                if (lastEstimate.Watts12 > 0)
                    smeta.Info.Add("Quvvat 12V: " + Num.Format(Math.Round(lastEstimate.Watts12, 1)) + " W");
                smeta.Info.AddRange(lastEstimate.Construction);
            }

            foreach (MaterialRow row in materialRows)
            {
                smeta.Rows.Add(new SmetaRow
                {
                    Group = MaterialGroups.Title(row.Line.Group),
                    Name = row.Line.Name,
                    Quantity = QuantityOf(row.Line),
                    Unit = row.Line.Unit,
                    Price = PriceOf(row.Line.PriceKey),
                    Note = row.Line.Note
                });
            }
            foreach (CustomRow row in store.CustomRows.Where(r => r.Quantity > 0 && !string.IsNullOrWhiteSpace(r.Name)))
            {
                smeta.Rows.Add(new SmetaRow
                {
                    Group = "QO'SHIMCHA XARAJATLAR",
                    Name = row.Name.Trim(),
                    Quantity = row.Quantity,
                    Unit = row.Unit,
                    Price = row.Price,
                    Custom = true
                });
            }
            return smeta;
        }

        // ================================================================ normalar

        private void BuildNormRows()
        {
            NormsPanel.Children.Clear();
            foreach (IGrouping<NormGroup, NormInfo> group in Norms.All.GroupBy(norm => norm.Group))
            {
                NormsPanel.Children.Add(new TextBlock { Style = (Style)FindResource("GroupTitle"), Text = NormGroupTitle(group.Key) });
                foreach (NormInfo info in group)
                    NormsPanel.Children.Add(CreateNormRow(info));
            }
        }

        private static string NormGroupTitle(NormGroup group)
        {
            switch (group)
            {
                case NormGroup.Sheets: return "LISTLAR, PLYONKA, PROFIL";
                case NormGroup.Electric: return "ELEKTR";
                default: return "MAHKAMLASH, KLEY, KRASKA";
            }
        }

        private UIElement CreateNormRow(NormInfo info)
        {
            var grid = new Grid { ToolTip = info.Hint, Margin = new Thickness(0, 1, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });

            grid.Children.Add(new TextBlock { Style = (Style)FindResource("FieldLabel"), Text = info.Title, TextWrapping = TextWrapping.Wrap });

            double value = store.Norms.TryGetValue(info.Key, out double saved) ? saved : info.Default;
            var box = new TextBox { Text = Num.Format(value) };
            box.TextChanged += (sender, e) =>
            {
                if (Num.TryParse(box.Text, out double number) && number >= info.Min && number <= info.Max)
                {
                    box.ClearValue(BorderBrushProperty);
                    if (Math.Abs(number - info.Default) < 1e-9)
                        store.Norms.Remove(info.Key);
                    else
                        store.Norms[info.Key] = number;
                    ScheduleSave();
                    ScheduleRefresh();
                }
                else
                {
                    box.BorderBrush = (Brush)FindResource("ErrorBrush");
                    box.ToolTip = string.Format("{0} … {1} oralig'ida son kiriting", Num.Format(info.Min), Num.Format(info.Max));
                }
            };
            Grid.SetColumn(box, 1);
            grid.Children.Add(box);

            var unitText = new TextBlock { Style = (Style)FindResource("Hint"), Text = info.Unit, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(unitText, 2);
            grid.Children.Add(unitText);
            return grid;
        }

        private void ResetNormsButton_Click(object sender, RoutedEventArgs e)
        {
            store.Norms.Clear();
            BuildNormRows();
            ScheduleSave();
            ScheduleRefresh();
        }

        // ================================================================ saqlash va eksport

        private void ScheduleSave()
        {
            if (saveTimer == null)
                return;
            saveTimer.Stop();
            saveTimer.Start();
        }

        private void SaveStore()
        {
            try
            {
                store.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReklamaRasxod: sozlamalarni saqlab bo'lmadi: " + ex);
            }
        }

        private void CsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (lastEstimate == null)
            {
                SetStatus("Avval dizaynni tanlang — hisob bo'sh.", true);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Smetani saqlash (Excel)",
                Filter = "CSV — Excel (*.csv)|*.csv",
                FileName = "Rasxod_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".csv"
            };
            if (dialog.ShowDialog() != true)
                return;

            try
            {
                EstimateExport.WriteCsv(dialog.FileName, BuildSmeta());
                SetStatus("Saqlandi: " + dialog.FileName, false);
                Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + dialog.FileName + "\"") { UseShellExecute = true });
            }
            catch (IOException ex)
            {
                SetStatus("Faylni yozib bo'lmadi (Excel'da ochiq bo'lishi mumkin): " + ex.Message, true);
            }
            catch (UnauthorizedAccessException ex)
            {
                SetStatus("Bu papkaga yozishga ruxsat yo'q: " + ex.Message, true);
            }
        }

        private void CorelTableButton_Click(object sender, RoutedEventArgs e)
        {
            if (lastEstimate == null)
            {
                SetStatus("Avval dizaynni tanlang — hisob bo'sh.", true);
                return;
            }

            RunCorelAction(() =>
            {
                EstimateExport.DrawInCorel(corelApp, corelService.RequireDocument(), BuildSmeta(), logo?.Info?.Shapes);
                SetStatus("Smeta Corelga qo'yildi (logoning o'ng tomonida). Bekor qilish — Ctrl+Z.", false);
            });
        }
    }
}
