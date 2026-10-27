using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReklamaMaket.Core;

namespace ReklamaMaket
{
    /// <summary>
    /// "Detallar" bo'limi: kant, distansiya, LED nuqta, nur, ramka, gul, reyka, gravirovka chizig'i, devor, boltlar —
    /// rangi va o'lchami. Ro'yxat tanlangan harf va fonga qarab quriladi; bo'sh qoldirilgani dizaynning o'z qiymatini oladi.
    /// </summary>
    public partial class DockerUI
    {
        private readonly Dictionary<Detail, Color> detailColors = new Dictionary<Detail, Color>();
        private readonly Dictionary<Detail, double> detailSizes = new Dictionary<Detail, double>(); // mm
        private readonly List<DetailRow> detailRows = new List<DetailRow>();
        private string detailSignature;

        private sealed class DetailRow
        {
            public DetailInfo Info;
            public Button Swatch;
            public TextBox Hex;
            public TextBox SizeBox;
            public TextBlock Placeholder;
            public TextBlock Unit;
            public Button Reset;
        }

        /// <summary>Dizayn o'zgarganda (harf, fon, gul) shu dizaynga xos foydalanuvchi qiymatlarini bekor qiladi.</summary>
        private void ResetDetails(params DetailReset[] reasons)
        {
            foreach (DetailInfo info in SignDetails.All.Where(d => reasons.Contains(d.Reset)))
                ClearDetail(info.Key);
        }

        private void ClearDetail(Detail detail)
        {
            detailColors.Remove(detail);
            detailSizes.Remove(detail);

            DetailRow row = detailRows.FirstOrDefault(r => r.Info.Key == detail);
            if (row?.SizeBox != null)
            {
                bool previous = suppressEvents;
                suppressEvents = true;
                row.SizeBox.Text = string.Empty;
                row.SizeBox.ClearValue(BorderBrushProperty);
                suppressEvents = previous;
            }
        }

        /// <summary>Ro'yxatni joriy dizaynga moslaydi va "avto" qiymatlarni yangilaydi (har ko'rinish yangilanishida).</summary>
        private void SyncDetails(SignSettings s)
        {
            List<DetailInfo> applicable = SignDetails.All.Where(d => SignDetails.Applies(s, d.Key)).ToList();
            string signature = string.Join(",", applicable.Select(d => SignDetails.Title(s, d.Key)));
            if (signature != detailSignature)
            {
                detailSignature = signature;
                BuildDetailRows(s, applicable);
            }
            UpdateDetailRows(s);
        }

        private void BuildDetailRows(SignSettings s, List<DetailInfo> applicable)
        {
            DetailsPanel.Children.Clear();
            detailRows.Clear();

            if (applicable.Count == 0)
            {
                DetailsPanel.Children.Add(new TextBlock
                {
                    Style = (Style)FindResource("Hint"),
                    Text = "Bu dizaynda alohida detal yo'q — ranglar va o'lchamlar yuqoridagi bo'limlarda."
                });
                return;
            }

            foreach (IGrouping<DetailGroup, DetailInfo> group in applicable.GroupBy(d => d.Group))
            {
                DetailsPanel.Children.Add(new TextBlock
                {
                    Text = GroupTitle(group.Key),
                    FontWeight = FontWeights.Bold,
                    FontSize = 10.5,
                    Foreground = (Brush)FindResource("AccentBrush"),
                    Margin = new Thickness(0, DetailsPanel.Children.Count == 0 ? 0 : 6, 0, 0)
                });
                foreach (DetailInfo info in group)
                    DetailsPanel.Children.Add(CreateDetailRow(s, info));
            }
        }

        private static string GroupTitle(DetailGroup group)
        {
            switch (group)
            {
                case DetailGroup.Logo: return "LOGO";
                case DetailGroup.Light: return "NUR (CHIROQ)";
                default: return "FON";
            }
        }

        private UIElement CreateDetailRow(SignSettings s, DetailInfo info)
        {
            var row = new DetailRow { Info = info };
            var panel = new StackPanel { Margin = new Thickness(0, 2, 0, 2), ToolTip = info.Hint };
            panel.Children.Add(new TextBlock { Style = (Style)FindResource("FieldLabel"), Text = SignDetails.Title(s, info.Key) });

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = info.HasColor ? (info.HasSize ? new GridLength(78) : new GridLength(1, GridUnitType.Star)) : new GridLength(0) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = info.HasSize ? new GridLength(1, GridUnitType.Star) : new GridLength(0) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (info.HasColor)
            {
                row.Swatch = new Button { Style = (Style)FindResource("Swatch"), Width = 28, Margin = new Thickness(0, 2, 6, 2), Tag = row, ToolTip = "Rang tanlash" };
                row.Swatch.Click += DetailSwatch_Click;
                grid.Children.Add(row.Swatch);

                row.Hex = new TextBox { Tag = row, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, info.HasSize ? 6 : 0, 0) };
                row.Hex.TextChanged += DetailHex_TextChanged;
                Grid.SetColumn(row.Hex, 1);
                grid.Children.Add(row.Hex);
            }

            if (info.HasSize)
            {
                var sizeHost = new Grid();
                row.SizeBox = new TextBox { Tag = row };
                if (detailSizes.TryGetValue(info.Key, out double mm))
                    row.SizeBox.Text = Num.Format(System.Math.Round(unit.FromMm(mm), 3));
                row.SizeBox.TextChanged += DetailSize_TextChanged;
                row.Placeholder = new TextBlock
                {
                    Style = (Style)FindResource("Hint"),
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                };
                sizeHost.Children.Add(row.SizeBox);
                sizeHost.Children.Add(row.Placeholder);
                Grid.SetColumn(sizeHost, 2);
                grid.Children.Add(sizeHost);

                row.Unit = new TextBlock { Style = (Style)FindResource("Hint"), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(row.Unit, 3);
                grid.Children.Add(row.Unit);
            }

            row.Reset = new Button
            {
                Style = (Style)FindResource("SecondaryButton"),
                Content = "↺",
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "Dizaynning o'z qiymatiga qaytarish",
                Tag = row
            };
            row.Reset.Click += DetailReset_Click;
            Grid.SetColumn(row.Reset, 4);
            grid.Children.Add(row.Reset);

            panel.Children.Add(grid);
            detailRows.Add(row);
            return panel;
        }

        private void UpdateDetailRows(SignSettings s)
        {
            bool previous = suppressEvents;
            suppressEvents = true;
            try
            {
                foreach (DetailRow row in detailRows)
                {
                    Detail key = row.Info.Key;
                    bool colorSet = detailColors.ContainsKey(key);
                    bool sizeSet = detailSizes.ContainsKey(key);

                    if (row.Swatch != null)
                    {
                        Color color = SignDetails.ColorOf(s, key);
                        row.Swatch.Background = new SolidColorBrush(color);
                        if (!row.Hex.IsKeyboardFocusWithin)
                            row.Hex.Text = ColorMath.ToHex(color);
                        row.Hex.Foreground = (Brush)FindResource(colorSet ? "TextBrush" : "MutedBrush");
                    }

                    if (row.SizeBox != null)
                    {
                        row.Unit.Text = unit.Name;
                        row.Placeholder.Text = "avto: " + Num.Format(System.Math.Round(unit.FromMm(SignDetails.DefaultSize(s, key)), 2));
                        row.Placeholder.Visibility = row.SizeBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                    }

                    row.Reset.Visibility = colorSet || sizeSet ? Visibility.Visible : Visibility.Hidden;
                }
            }
            finally
            {
                suppressEvents = previous;
            }
        }

        /// <summary>Birlik (mm / sm / m) o'zgarganda kiritilgan o'lchamlarni yangi birlikda yozadi.</summary>
        private void WriteDetailSizes()
        {
            bool previous = suppressEvents;
            suppressEvents = true;
            foreach (DetailRow row in detailRows.Where(r => r.SizeBox != null))
            {
                row.SizeBox.Text = detailSizes.TryGetValue(row.Info.Key, out double mm)
                    ? Num.Format(System.Math.Round(unit.FromMm(mm), 3))
                    : string.Empty;
            }
            suppressEvents = previous;
        }

        private void DetailSwatch_Click(object sender, RoutedEventArgs e)
        {
            var row = (DetailRow)((FrameworkElement)sender).Tag;
            Color color = (row.Swatch.Background as SolidColorBrush)?.Color ?? Colors.White;
            if (PickColor(ref color))
            {
                detailColors[row.Info.Key] = color;
                row.Hex.Text = ColorMath.ToHex(color);
                ScheduleRefresh();
            }
        }

        private void DetailHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            var row = (DetailRow)((TextBox)sender).Tag;
            if (suppressEvents || !ColorMath.TryParseHex(row.Hex.Text, out Color color))
                return;

            detailColors[row.Info.Key] = color;
            row.Swatch.Background = new SolidColorBrush(color);
            ScheduleRefresh();
        }

        private void DetailSize_TextChanged(object sender, TextChangedEventArgs e)
        {
            var row = (DetailRow)((TextBox)sender).Tag;
            row.Placeholder.Visibility = row.SizeBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (suppressEvents)
                return;

            string text = row.SizeBox.Text.Trim();
            if (text.Length == 0)
            {
                detailSizes.Remove(row.Info.Key);
                row.SizeBox.ClearValue(BorderBrushProperty);
            }
            else if (Num.TryParse(text, out double value) && unit.ToMm(value) >= row.Info.MinSize && unit.ToMm(value) <= 100000)
            {
                detailSizes[row.Info.Key] = unit.ToMm(value);
                row.SizeBox.ClearValue(BorderBrushProperty);
            }
            else
            {
                // Noto'g'ri qiymat — oldingisi qoladi, maydon qizil
                row.SizeBox.BorderBrush = (Brush)FindResource("ErrorBrush");
                return;
            }
            ScheduleRefresh();
        }

        private void DetailReset_Click(object sender, RoutedEventArgs e)
        {
            var row = (DetailRow)((FrameworkElement)sender).Tag;
            ClearDetail(row.Info.Key);
            ScheduleRefresh();
        }

        private void PatternCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressEvents)
                return;

            // Yangi gul — o'zining qadami bilan
            ResetDetails(DetailReset.Pattern);
            ScheduleRefresh();
        }
    }
}
