using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;
using ReklamaMaket.Rendering;
using ReklamaRasxod.Core;
using corel = Corel.Interop.VGCore;
using Forms = System.Windows.Forms;

namespace ReklamaRasxod
{
    /// <summary>
    /// CorelDRAW paneli (docker). Corel uni AppUI.xslt dagi "wpfhost" orqali yaratadi
    /// va konstruktorga Application obyektini beradi. Dizayn tanlash qismi Reklama Maket bilan bir xil,
    /// rasxod jadvali — DockerUI.Estimate.cs da.
    /// </summary>
    public partial class DockerUI : UserControl
    {
        private readonly corel.Application corelApp;
        private readonly CorelService corelService;
        private readonly RasxodSettings store;
        private readonly DispatcherTimer refreshTimer;
        private readonly DispatcherTimer saveTimer;

        private LogoCapture logo;
        private LengthUnit unit = LengthUnit.Millimeter;
        private double logoAspect; // eni / bo'yi; 0 — logo hali olinmagan
        private Color faceColor = Colors.White;
        private Color returnColor = Colors.Gray;
        private Color lightColor = Colors.White;
        private Color backgroundColor = Colors.White;
        private bool suppressEvents = true;
        private bool showingValidationError;

        public DockerUI(object app)
        {
            InitializeComponent();

            corelApp = app as corel.Application;
            corelService = new CorelService(corelApp);
            store = RasxodSettings.Load();

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            refreshTimer.Tick += (sender, e) =>
            {
                refreshTimer.Stop();
                RefreshAll();
            };
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            saveTimer.Tick += (sender, e) =>
            {
                saveTimer.Stop();
                SaveStore();
            };

            SetupLists();
            SetupEstimate();

            WriteLength(LogoHeightBox, 600);
            WriteLength(LogoWidthBox, 2000);
            WriteLength(MarginBox, 150);
            UpdateUnitTexts();

            LetterCombo.SelectedIndex = 0;
            BackgroundCombo.SelectedIndex = 0;
            ApplyLetterStyle(LetterStyles.All[0]);
            ApplyBackgroundStyle(BackgroundStyles.All[0]);
            UpdateImportInfo();
            // Reklama Maket'da yangi dizayn chizilgan bo'lsa, yozuv yangilansin
            MouseEnter += (sender, e) => UpdateImportInfo();

            suppressEvents = false;
            RefreshAll();

            if (corelApp == null)
                SetStatus("CorelDRAW bilan bog'lanib bo'lmadi (VGCore).", true);
            else
                SetStatus("Corelda logoni belgilang va \"Tanlangan logoni olish\"ni bosing — yoki Reklama Maket dizaynini oling.", false);
        }

        private TextBox[] LengthBoxes => new[]
        {
            LogoHeightBox, LogoWidthBox, DepthBox, BackgroundWidthBox, BackgroundHeightBox, MarginBox, BackgroundDepthBox
        };

        // ================================================================ boshlang'ich sozlash

        private void SetupLists()
        {
            // Ro'yxat ikki guruhga bo'linadi: hajmli harflar va fonga o'yilgan logo.
            var letters = new ListCollectionView(LetterStyles.All);
            letters.GroupDescriptions.Add(new PropertyGroupDescription(nameof(LetterStyle.Group)));
            LetterCombo.ItemsSource = letters;
            BackgroundCombo.ItemsSource = BackgroundStyles.All;

            UnitCombo.ItemsSource = LengthUnit.All;
            UnitCombo.SelectedItem = unit;

            AlignCombo.ItemsSource = new[]
            {
                new Choice<LogoAlign>("Markazda", LogoAlign.Center),
                new Choice<LogoAlign>("Chapda", LogoAlign.Left),
                new Choice<LogoAlign>("O'ngda", LogoAlign.Right),
                new Choice<LogoAlign>("Tepada", LogoAlign.Top),
                new Choice<LogoAlign>("Pastda", LogoAlign.Bottom),
            };
            AlignCombo.SelectedIndex = 0;

            PatternCombo.ItemsSource = PatternStyles.All.Select(pattern => new PatternItem(pattern)).ToList();
            BackgroundLightCombo.ItemsSource = BackgroundLights.All
                .Select(light => new Choice<BackgroundLight>(BackgroundLights.Name(light), light)).ToList();
        }

        private void ApplyLetterStyle(LetterStyle style)
        {
            ResetDetails(DetailReset.Letter);
            ClearOverrides();
            faceColor = style.FaceColor;
            returnColor = style.ReturnColor;
            lightColor = style.LightColor;

            bool previous = suppressEvents;
            suppressEvents = true;
            try
            {
                WriteLength(DepthBox, style.DefaultDepth);
                LetterDescription.Text = style.Description + (style.Illuminated ? string.Empty : " Yoritilmaydi.");
                DepthLabel.Text = style.DepthLabel;
                FaceColorLabel.Text = style.FaceColorLabel;
                ReturnColorLabel.Text = style.ReturnColorLabel;

                // O'yilgan logoda bort yo'q (push-through'dan tashqari) — qator yashiriladi.
                Visibility returnRow = style.ReturnColorLabel == "—" ? Visibility.Collapsed : Visibility.Visible;
                ReturnColorLabel.Visibility = returnRow;
                ReturnButton.Visibility = returnRow;
                ReturnHex.Visibility = returnRow;
                KeepColorsCheck.IsEnabled = !style.IsCut || style.Cut == CutMode.RoutedLit;

                if (style.IsCut && BackgroundCombo.SelectedItem is BackgroundStyle background && !background.CanBeCut)
                    SelectCutBackground(style);

                UpdateColorControls();
            }
            finally
            {
                suppressEvents = previous;
            }
            ScheduleRefresh();
        }

        /// <summary>O'yilgan logo uchun fonni alyukobondga almashtiradi: yonadigan — quti, yonmaydigan — panel.</summary>
        private void SelectCutBackground(LetterStyle style)
        {
            BackgroundKind kind = style.Illuminated ? BackgroundKind.AcpBox : BackgroundKind.Panel;
            BackgroundStyle background = BackgroundStyles.All.First(b => b.Kind == kind);
            BackgroundCombo.SelectedItem = background;
            ApplyBackgroundStyle(background);
            SetStatus("O'yilgan logo uchun fon \"" + background.Name + "\" ga almashtirildi.", false);
        }

        private void ApplyBackgroundStyle(BackgroundStyle style)
        {
            ResetDetails(DetailReset.Background, DetailReset.Pattern);
            ClearOverrides();
            backgroundColor = style.DefaultColor;

            bool previous = suppressEvents;
            suppressEvents = true;
            try
            {
                WriteLength(BackgroundDepthBox, style.DefaultDepth);
                BackgroundDescription.Text = style.Description;
                PatternPanel.Visibility = style.HasPattern ? Visibility.Visible : Visibility.Collapsed;
                LightingPanel.Visibility = style.SupportsLighting ? Visibility.Visible : Visibility.Collapsed;
                EngravedBorderCheck.Visibility = style.CanHaveBorder ? Visibility.Visible : Visibility.Collapsed;
                PatternCombo.SelectedIndex = style.DefaultPattern;
                BackgroundLightCombo.SelectedIndex = Array.IndexOf(BackgroundLights.All, style.DefaultLight);
                AutoSizeCheck.IsEnabled = style.Kind != BackgroundKind.Wall;
                UpdateBackgroundSizeEnabled();
                UpdateColorControls();
            }
            finally
            {
                suppressEvents = previous;
            }
            ScheduleRefresh();
        }

        private void UpdateBackgroundSizeEnabled()
        {
            var background = BackgroundCombo.SelectedItem as BackgroundStyle;
            bool manual = background != null && background.Kind != BackgroundKind.Wall && AutoSizeCheck.IsChecked != true;
            BackgroundWidthBox.IsEnabled = manual;
            BackgroundHeightBox.IsEnabled = manual;
        }

        // ================================================================ sozlamalarni o'qish/yozish

        private void WriteLength(TextBox box, double mm) => box.Text = Num.Format(Math.Round(unit.FromMm(mm), 3));

        private SignSettings ReadSettings(out string error)
        {
            if (!ReadLength(LogoHeightBox, "Logo bo'yi", 10, out double logoHeight, out error) ||
                !ReadLength(LogoWidthBox, "Logo eni", 10, out double logoWidth, out error) ||
                !ReadLength(DepthBox, "Qalinligi", 1, out double depth, out error) ||
                !ReadLength(MarginBox, "Chetdan joy", 0, out double margin, out error) ||
                !ReadLength(BackgroundDepthBox, "Fon qalinligi", 0, out double backgroundDepth, out error))
            {
                return null;
            }

            var background = BackgroundCombo.SelectedItem as BackgroundStyle ?? BackgroundStyles.All[0];
            bool auto = AutoSizeCheck.IsChecked == true || background.Kind == BackgroundKind.Wall;
            double backgroundWidth = 0, backgroundHeight = 0;
            if (!auto &&
                (!ReadLength(BackgroundWidthBox, "Fon eni", 10, out backgroundWidth, out error) ||
                 !ReadLength(BackgroundHeightBox, "Fon bo'yi", 10, out backgroundHeight, out error)))
            {
                return null;
            }

            var align = AlignCombo.SelectedItem as Choice<LogoAlign>;
            var pattern = PatternCombo.SelectedItem as PatternItem;
            var backgroundLight = BackgroundLightCombo.SelectedItem as Choice<BackgroundLight>;
            return new SignSettings
            {
                Pattern = pattern != null ? pattern.Style : PatternStyles.All[background.DefaultPattern],
                BackgroundLight = background.SupportsLighting && backgroundLight != null ? backgroundLight.Value : BackgroundLight.None,
                Letter = LetterCombo.SelectedItem as LetterStyle ?? LetterStyles.All[0],
                LogoWidth = logoWidth,
                LogoHeight = logoHeight,
                Depth = depth,
                FaceColor = faceColor,
                ReturnColor = returnColor,
                LightColor = lightColor,
                KeepLogoColors = KeepColorsCheck.IsChecked == true,
                Background = background,
                BackgroundColor = backgroundColor,
                EngravedBorder = background.CanHaveBorder && EngravedBorderCheck.IsChecked == true,
                DetailColors = new Dictionary<Detail, Color>(detailColors),
                DetailSizes = new Dictionary<Detail, double>(detailSizes),
                AutoBackgroundSize = auto,
                BackgroundWidth = backgroundWidth,
                BackgroundHeight = backgroundHeight,
                BackgroundDepth = backgroundDepth,
                Margin = margin,
                Align = align != null ? align.Value : LogoAlign.Center
            };
        }

        private bool ReadLength(TextBox box, string name, double minMm, out double mm, out string error)
        {
            mm = 0;
            error = null;

            if (!Num.TryParse(box.Text, out double value))
            {
                error = name + ": son kiriting.";
            }
            else
            {
                mm = unit.ToMm(value);
                if (mm < minMm)
                    error = string.Format("{0} kamida {1} {2} bo'lishi kerak.", name, Num.Format(unit.FromMm(minMm)), unit);
                else if (mm > 100000)
                    error = name + " juda katta (100 m dan oshmasin).";
            }

            if (error != null)
            {
                box.BorderBrush = (Brush)FindResource("ErrorBrush");
                return false;
            }

            box.ClearValue(BorderBrushProperty);
            return true;
        }

        // ================================================================ ko'rinish va hisob

        private void ScheduleRefresh()
        {
            if (suppressEvents || refreshTimer == null)
                return;

            refreshTimer.Stop();
            refreshTimer.Start();
        }

        private void RefreshAll()
        {
            SignSettings settings = ReadSettings(out string error);
            if (settings == null)
            {
                SetStatus(error, true);
                showingValidationError = true;
                return;
            }

            if (showingValidationError)
            {
                SetStatus(string.Empty, false);
                showingValidationError = false;
            }

            RuleResult rules = SignRules.Check(settings);
            bool night = NightRadio.IsChecked == true;
            SyncDetails(settings);
            try
            {
                PreviewBox.Child = PreviewBuilder.Build(settings, rules, logo?.Info, night);
            }
            catch (Exception ex)
            {
                SetStatus("Ko'rinishni chizishda xato: " + ex.Message, true);
            }
            PreviewHost.Background = (Brush)FindResource(night ? "NightBrush" : "CheckerBrush");

            SignLayout layout = LogoLayout.Compute(settings);
            SizeInfoText.Text = string.Format("Logo {0} × {1} mm · fon {2} × {3} mm · qalinligi {4} mm",
                Num.Format(Math.Round(settings.LogoWidth)), Num.Format(Math.Round(settings.LogoHeight)),
                Num.Format(Math.Round(layout.BackgroundWidth)), Num.Format(Math.Round(layout.BackgroundHeight)),
                Num.Format(settings.Depth));

            WarningBox.Visibility = rules.HasWarnings ? Visibility.Visible : Visibility.Collapsed;
            WarningText.Text = string.Join("\n", rules.Warnings.Select(w => "⚠ " + w));

            if (settings.AutoBackgroundSize)
            {
                // Qo'lda o'lchamga o'tilganda shu qiymatlardan boshlanadi
                bool previous = suppressEvents;
                suppressEvents = true;
                WriteLength(BackgroundWidthBox, layout.BackgroundWidth);
                WriteLength(BackgroundHeightBox, layout.BackgroundHeight);
                suppressEvents = previous;
            }

            try
            {
                Recalculate(settings);
            }
            catch (Exception ex)
            {
                SetStatus("Rasxodni hisoblashda xato: " + ex.Message, true);
            }
        }

        private void UpdateUnitTexts()
        {
            foreach (TextBlock label in new[] { LogoHeightUnit, LogoWidthUnit, DepthUnit, BackgroundWidthUnit, BackgroundHeightUnit, MarginUnit, BackgroundDepthUnit })
                label.Text = unit.Name;
        }

        private void UpdateColorControls()
        {
            bool previous = suppressEvents;
            suppressEvents = true;
            try
            {
                foreach (string target in new[] { "Face", "Return", "Light", "Background" })
                {
                    Color color = GetColor(target);
                    ButtonFor(target).Background = new SolidColorBrush(color);
                    HexFor(target).Text = ColorMath.ToHex(color);
                }
            }
            finally
            {
                suppressEvents = previous;
            }
        }

        private void SetStatus(string text, bool isError)
        {
            StatusText.Text = text;
            StatusText.Foreground = (Brush)FindResource(isError ? "ErrorBrush" : "MutedBrush");
        }

        // ================================================================ hodisalar: sozlamalar

        private void Setting_Changed(object sender, RoutedEventArgs e) => ScheduleRefresh();

        private void LogoHeight_Changed(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents)
                return;

            if (logoAspect > 0 && Num.TryParse(LogoHeightBox.Text, out double height) && height > 0)
            {
                suppressEvents = true;
                LogoWidthBox.Text = Num.Format(Math.Round(height * logoAspect, 2));
                suppressEvents = false;
            }
            ScheduleRefresh();
        }

        private void LogoWidth_Changed(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents)
                return;

            if (logoAspect > 0 && Num.TryParse(LogoWidthBox.Text, out double width) && width > 0)
            {
                suppressEvents = true;
                LogoHeightBox.Text = Num.Format(Math.Round(width / logoAspect, 2));
                suppressEvents = false;
            }
            ScheduleRefresh();
        }

        private void LetterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!suppressEvents && LetterCombo.SelectedItem is LetterStyle style)
                ApplyLetterStyle(style);
        }

        private void BackgroundCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!suppressEvents && BackgroundCombo.SelectedItem is BackgroundStyle style)
                ApplyBackgroundStyle(style);
        }

        private void AutoSize_Changed(object sender, RoutedEventArgs e)
        {
            if (suppressEvents)
                return;

            UpdateBackgroundSizeEnabled();
            ScheduleRefresh();
        }

        private void UnitCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressEvents || !(UnitCombo.SelectedItem is LengthUnit newUnit) || newUnit == unit)
                return;

            LengthUnit oldUnit = unit;
            unit = newUnit;

            suppressEvents = true;
            try
            {
                foreach (TextBox box in LengthBoxes)
                {
                    if (Num.TryParse(box.Text, out double value))
                        box.Text = Num.Format(Math.Round(newUnit.FromMm(oldUnit.ToMm(value)), 3));
                }
                UpdateUnitTexts();
                WriteDetailSizes();
            }
            finally
            {
                suppressEvents = false;
            }
            ScheduleRefresh();
        }

        // ================================================================ hodisalar: ranglar

        private Color GetColor(string target)
        {
            switch (target)
            {
                case "Face": return faceColor;
                case "Return": return returnColor;
                case "Light": return lightColor;
                default: return backgroundColor;
            }
        }

        private void SetColor(string target, Color color)
        {
            switch (target)
            {
                case "Face": faceColor = color; break;
                case "Return": returnColor = color; break;
                case "Light": lightColor = color; break;
                default: backgroundColor = color; break;
            }
        }

        private Button ButtonFor(string target)
        {
            switch (target)
            {
                case "Face": return FaceButton;
                case "Return": return ReturnButton;
                case "Light": return LightButton;
                default: return BackgroundButton;
            }
        }

        private TextBox HexFor(string target)
        {
            switch (target)
            {
                case "Face": return FaceHex;
                case "Return": return ReturnHex;
                case "Light": return LightHex;
                default: return BackgroundHex;
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            string target = (string)((FrameworkElement)sender).Tag;
            Color color = GetColor(target);
            if (PickColor(ref color))
            {
                SetColor(target, color);
                UpdateColorControls();
                ScheduleRefresh();
            }
        }

        private void Hex_TextChanged(object sender, TextChangedEventArgs e)
        {
            var box = (TextBox)sender;
            if (suppressEvents || !ColorMath.TryParseHex(box.Text, out Color color))
                return;

            string target = (string)box.Tag;
            SetColor(target, color);
            ButtonFor(target).Background = new SolidColorBrush(color);
            ScheduleRefresh();
        }

        private static bool PickColor(ref Color color)
        {
            using (var dialog = new Forms.ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B)
            })
            {
                if (dialog.ShowDialog() != Forms.DialogResult.OK)
                    return false;

                color = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                return true;
            }
        }

        // ================================================================ logo

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            RunCorelAction(() =>
            {
                SetLogo(LogoCapture.FromSelection(corelService), true);
                SetStatus("Logo olindi va o'lchandi. Endi harf va fon dizaynini tanlang.", false);
            });
        }

        /// <summary>Logoni o'rnatadi; keepHeight — bo'yi saqlanib, eni logo proporsiyasidan hisoblanadi.</summary>
        private void SetLogo(LogoCapture capture, bool keepHeight)
        {
            logo = capture;
            logoAspect = capture.Info.WidthMm / capture.Info.HeightMm;
            LogoImage.Source = capture.Info.Preview;
            LogoInfoText.Text = capture.Info.Count > 0
                ? string.Format("{0} ta obyekt\nCorelda: {1} × {2} mm", capture.Info.Count,
                    Num.Format(Math.Round(capture.Info.WidthMm, 1)), Num.Format(Math.Round(capture.Info.HeightMm, 1)))
                : "Reklama Maket'dan olingan logo";
            if (capture.Opaque)
                LogoInfoText.Text += "\nRasm shaffof emas — o'lchov taxminiy";

            if (keepHeight && Num.TryParse(LogoHeightBox.Text, out double height) && height > 0)
            {
                bool previous = suppressEvents;
                suppressEvents = true;
                LogoWidthBox.Text = Num.Format(Math.Round(height * logoAspect, 2));
                suppressEvents = previous;
            }
            ScheduleRefresh();
        }

        private void UpdateImportInfo()
        {
            DesignSnapshot snapshot = DesignSnapshot.Load();
            ImportInfoText.Text = snapshot == null
                ? "Reklama Maket'da dizayn chizilgach, shu yerdan olish mumkin."
                : string.Format("Oxirgi dizayn: {0} ({1:dd.MM.yyyy HH:mm})", snapshot.FindLetter()?.Name ?? snapshot.Letter, snapshot.Saved);
        }

        /// <summary>Reklama Maket'da oxirgi chizilgan dizayn: harf, fon, o'lchamlar, ranglar, detallar va logo.</summary>
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            DesignSnapshot snapshot = DesignSnapshot.Load();
            UpdateImportInfo();
            if (snapshot == null)
            {
                SetStatus("Reklama Maket'da saqlangan dizayn topilmadi. Avval u yerda \"CORELDA DIZAYN CHIZISH\"ni bosing.", true);
                return;
            }

            LetterStyle letter = snapshot.FindLetter();
            BackgroundStyle background = snapshot.FindBackground();
            if (letter == null || background == null)
            {
                SetStatus("Saqlangan dizayn turi bu versiyada yo'q. Ikkala plaginni bir xil versiyaga yangilang.", true);
                return;
            }

            LogoCapture capture = null;
            try
            {
                BitmapSource bitmap = snapshot.LoadLogo();
                if (bitmap != null && snapshot.LogoCorelWidth > 0 && snapshot.LogoCorelHeight > 0)
                    capture = LogoCapture.FromBitmap(bitmap, snapshot.LogoCorelWidth, snapshot.LogoCorelHeight, 0, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReklamaRasxod: logo rasmini o'qib bo'lmadi: " + ex);
            }

            suppressEvents = true;
            try
            {
                LetterCombo.SelectedItem = letter;
                ApplyLetterStyle(letter);
                BackgroundCombo.SelectedItem = background;
                ApplyBackgroundStyle(background);

                PatternStyle pattern = snapshot.FindPattern();
                if (pattern != null)
                    PatternCombo.SelectedIndex = Array.IndexOf(PatternStyles.All, pattern);
                if (Enum.TryParse(snapshot.BackgroundLight, out BackgroundLight light))
                    BackgroundLightCombo.SelectedIndex = Array.IndexOf(BackgroundLights.All, light);
                if (Enum.TryParse(snapshot.Align, out LogoAlign align))
                    AlignCombo.SelectedItem = AlignCombo.Items.Cast<Choice<LogoAlign>>().FirstOrDefault(c => c.Value == align);

                faceColor = DesignSnapshot.ParseColor(snapshot.FaceColor, faceColor);
                returnColor = DesignSnapshot.ParseColor(snapshot.ReturnColor, returnColor);
                lightColor = DesignSnapshot.ParseColor(snapshot.LightColor, lightColor);
                backgroundColor = DesignSnapshot.ParseColor(snapshot.BackgroundColor, backgroundColor);
                UpdateColorControls();
                KeepColorsCheck.IsChecked = snapshot.KeepLogoColors;
                EngravedBorderCheck.IsChecked = snapshot.EngravedBorder;
                AutoSizeCheck.IsChecked = snapshot.AutoBackgroundSize;
                UpdateBackgroundSizeEnabled();

                WriteLength(LogoHeightBox, snapshot.LogoHeight);
                WriteLength(LogoWidthBox, snapshot.LogoWidth);
                WriteLength(DepthBox, snapshot.Depth);
                WriteLength(MarginBox, snapshot.Margin);
                WriteLength(BackgroundDepthBox, snapshot.BackgroundDepth);
                if (!snapshot.AutoBackgroundSize)
                {
                    WriteLength(BackgroundWidthBox, snapshot.BackgroundWidth);
                    WriteLength(BackgroundHeightBox, snapshot.BackgroundHeight);
                }

                detailColors.Clear();
                foreach (KeyValuePair<Detail, Color> pair in snapshot.ParseDetailColors())
                    detailColors[pair.Key] = pair.Value;
                detailSizes.Clear();
                foreach (KeyValuePair<Detail, double> pair in snapshot.ParseDetailSizes())
                    detailSizes[pair.Key] = pair.Value;
                detailSignature = null; // detallar qatori yangi qiymatlar bilan qayta quriladi

                if (capture != null)
                    SetLogo(capture, false);
            }
            finally
            {
                suppressEvents = false;
            }

            RefreshAll();
            SetStatus(capture != null
                ? "Reklama Maket dizayni olindi: " + letter.Name + " + " + background.Name + "."
                : "Dizayn olindi, lekin logo rasmi yo'q — Corelda logoni belgilab \"Tanlangan logoni olish\"ni bosing.", capture == null);
        }

        private void RunCorelAction(Action action)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                action();
            }
            catch (UserException ex)
            {
                SetStatus(ex.Message, true);
            }
            catch (Exception ex)
            {
                SetStatus("Xatolik: " + ex.Message, true);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void DockerUI_Unloaded(object sender, RoutedEventArgs e)
        {
            refreshTimer.Stop();
            saveTimer.Stop();
            SaveStore();
        }

        /// <summary>Gul ro'yxati elementi: nom + kichik rasm.</summary>
        private sealed class PatternItem
        {
            public PatternItem(PatternStyle style)
            {
                Style = style;
                Thumbnail = PreviewBuilder.PatternThumbnail(style);
            }

            public PatternStyle Style { get; }
            public string Name => Style.Name;
            public string EnglishName => Style.EnglishName;
            public ImageSource Thumbnail { get; }
        }

        /// <summary>ComboBox uchun nom + qiymat.</summary>
        private sealed class Choice<T>
        {
            public Choice(string name, T value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }
            public T Value { get; }
        }
    }
}
