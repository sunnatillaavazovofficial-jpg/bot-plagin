using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using ReklamaMaket.Ai;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;
using ReklamaMaket.Rendering;
using corel = Corel.Interop.VGCore;
using Forms = System.Windows.Forms;

namespace ReklamaMaket
{
    /// <summary>
    /// CorelDRAW paneli (docker). Corel uni AppUI.xslt dagi "wpfhost" orqali yaratadi
    /// va konstruktorga Application obyektini beradi.
    /// </summary>
    public partial class DockerUI : UserControl
    {
        private const int AiImagePixels = 1536;
        private const long MaxPhotoBytes = 45L * 1024 * 1024;

        private static readonly Color[] BackgroundPresets =
        {
            Colors.White,
            ColorMath.Rgb(0x1E2430),
            ColorMath.Rgb(0x111111),
            ColorMath.Rgb(0xD8CFC2),
            ColorMath.Rgb(0xE30613),
            ColorMath.Rgb(0x0057B8),
            ColorMath.Rgb(0xFFD400),
            ColorMath.Rgb(0x00963F),
            ColorMath.Rgb(0x8A8D91),
        };

        private static readonly Color[] LightPresets =
        {
            Colors.White,              // sovuq oq 6500K
            ColorMath.Rgb(0xFFC861),   // iliq oq 3000K
            ColorMath.Rgb(0xFF3B30),
            ColorMath.Rgb(0x2F7BFF),
            ColorMath.Rgb(0x34C759),
            ColorMath.Rgb(0xFF2DCE),
        };

        private readonly corel.Application corelApp;
        private readonly CorelService corelService;
        private readonly AppSettings appSettings;
        private readonly DispatcherTimer refreshTimer;

        private LogoInfo logo;
        private DrawResult lastResult;
        private LengthUnit unit = LengthUnit.Millimeter;
        private double logoAspect; // eni / bo'yi; 0 — logo hali olinmagan
        private Color faceColor = Colors.White;
        private Color returnColor = Colors.Gray;
        private Color lightColor = Colors.White;
        private Color backgroundColor = Colors.White;
        private string photoPath;
        private string lastAiFolder;
        private CancellationTokenSource aiCancel;
        private bool suppressEvents = true;
        private bool showingValidationError;

        public DockerUI(object app)
        {
            InitializeComponent();

            corelApp = app as corel.Application;
            corelService = new CorelService(corelApp);
            appSettings = AppSettings.Load();

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            refreshTimer.Tick += (sender, e) =>
            {
                refreshTimer.Stop();
                RefreshPreview();
            };

            ApiKeyLink.NavigateUri = new Uri(OpenAiImageClient.ApiKeysUrl);
            SetupLists();
            LoadAiSettings();

            WriteLength(LogoHeightBox, 600);
            WriteLength(LogoWidthBox, 2000);
            WriteLength(MarginBox, 150);
            UpdateUnitTexts();

            LetterCombo.SelectedIndex = 0;
            BackgroundCombo.SelectedIndex = 0;
            ApplyLetterStyle(LetterStyles.All[0]);
            ApplyBackgroundStyle(BackgroundStyles.All[0]);

            suppressEvents = false;
            RefreshPreview();

            if (corelApp == null)
                SetStatus("CorelDRAW bilan bog'lanib bo'lmadi (VGCore).", true);
            else
                SetStatus("Corelda logoni belgilang va \"Tanlangan logoni olish\" tugmasini bosing.", false);
        }

        private TextBox[] LengthBoxes => new[]
        {
            LogoHeightBox, LogoWidthBox, DepthBox, BackgroundWidthBox, BackgroundHeightBox, MarginBox, BackgroundDepthBox
        };

        // ================================================================ boshlang'ich sozlash

        private void SetupLists()
        {
            LetterCombo.ItemsSource = LetterStyles.All;
            BackgroundCombo.ItemsSource = BackgroundStyles.All;

            UnitCombo.ItemsSource = LengthUnit.All;
            UnitCombo.SelectedItem = unit;

            ScaleCombo.ItemsSource = new[] { 1, 5, 10, 20, 50, 100 };
            ScaleCombo.SelectedItem = 10;

            AlignCombo.ItemsSource = new[]
            {
                new Choice<LogoAlign>("Markazda", LogoAlign.Center),
                new Choice<LogoAlign>("Chapda", LogoAlign.Left),
                new Choice<LogoAlign>("O'ngda", LogoAlign.Right),
                new Choice<LogoAlign>("Tepada", LogoAlign.Top),
                new Choice<LogoAlign>("Pastda", LogoAlign.Bottom),
            };
            AlignCombo.SelectedIndex = 0;

            AddSwatches(BackgroundSwatches, BackgroundPresets, "Background");
            AddSwatches(LightSwatches, LightPresets, "Light");
        }

        private void AddSwatches(Panel panel, IEnumerable<Color> colors, string target)
        {
            var style = (Style)FindResource("Swatch");
            foreach (Color color in colors)
            {
                var swatch = new Button
                {
                    Style = style,
                    Background = new SolidColorBrush(color),
                    ToolTip = ColorMath.ToHex(color),
                    Tag = new KeyValuePair<string, Color>(target, color)
                };
                swatch.Click += Swatch_Click;
                panel.Children.Add(swatch);
            }
        }

        private void ApplyLetterStyle(LetterStyle style)
        {
            faceColor = style.FaceColor;
            returnColor = style.ReturnColor;
            lightColor = style.LightColor;

            bool previous = suppressEvents;
            suppressEvents = true;
            try
            {
                WriteLength(DepthBox, style.DefaultDepth);
                LetterDescription.Text = style.Description + (style.Illuminated ? string.Empty : " Yoritilmaydi.");
                LightButton.IsEnabled = style.Illuminated;
                LightHex.IsEnabled = style.Illuminated;
                LightSwatches.IsEnabled = style.Illuminated;
                UpdateColorControls();
            }
            finally
            {
                suppressEvents = previous;
            }
            ScheduleRefresh();
        }

        private void ApplyBackgroundStyle(BackgroundStyle style)
        {
            backgroundColor = style.DefaultColor;

            bool previous = suppressEvents;
            suppressEvents = true;
            try
            {
                WriteLength(BackgroundDepthBox, style.DefaultDepth);
                BackgroundDescription.Text = style.Description;
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
            return new SignSettings
            {
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
                AutoBackgroundSize = auto,
                BackgroundWidth = backgroundWidth,
                BackgroundHeight = backgroundHeight,
                BackgroundDepth = backgroundDepth,
                Margin = margin,
                Align = align != null ? align.Value : LogoAlign.Center,
                Scale = ScaleCombo.SelectedItem is int scale ? scale : 10,
                DrawNight = NightCheck.IsChecked == true,
                DrawSection = SectionCheck.IsChecked == true,
                DrawDimensions = DimensionsCheck.IsChecked == true,
                NewPage = NewPageCheck.IsChecked == true
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

        // ================================================================ ko'rinish

        private void ScheduleRefresh()
        {
            if (suppressEvents || refreshTimer == null)
                return;

            refreshTimer.Stop();
            refreshTimer.Start();
        }

        private void RefreshPreview()
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
            try
            {
                PreviewBox.Child = PreviewBuilder.Build(settings, rules, logo, night);
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

        private void SetAiStatus(string text, bool isError)
        {
            AiStatusText.Text = text;
            AiStatusText.Foreground = (Brush)FindResource(isError ? "ErrorBrush" : "MutedBrush");
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

        private void Swatch_Click(object sender, RoutedEventArgs e)
        {
            var tag = (KeyValuePair<string, Color>)((Button)sender).Tag;
            SetColor(tag.Key, tag.Value);
            UpdateColorControls();
            ScheduleRefresh();
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

        // ================================================================ hodisalar: Corel

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            RunCorelAction(() =>
            {
                CaptureLogo();
                SetStatus("Logo olindi. Endi harf va fon dizaynini tanlang.", false);
                return true;
            });
        }

        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureSynchronizationContext();
            bool drawn = RunCorelAction(() =>
            {
                corel.Document doc = corelService.RequireDocument();
                if (logo == null)
                {
                    if (!corelService.HasSelection())
                        throw new UserException("Avval Corelda logoni belgilang va \"Tanlangan logoni olish\" tugmasini bosing.");
                    CaptureLogo();
                }

                SignSettings settings = ReadSettings(out string error);
                if (settings == null)
                    throw new UserException(error);

                RuleResult rules = SignRules.Check(settings);
                if (rules.HasWarnings && !ConfirmWarnings(rules))
                {
                    SetStatus("Bekor qilindi.", false);
                    return false;
                }

                lastResult = new ChannelLetterRenderer(corelApp).Draw(doc, settings, logo);
                SetStatus(string.Format("Tayyor: \"{0}\" dizayni {1} chizildi. Bekor qilish — Ctrl+Z.",
                    settings.Letter.Name, settings.NewPage ? "yangi sahifada" : "joriy sahifada"), false);
                return true;
            });

            if (drawn && AutoSendCheck.IsChecked == true)
                await GenerateAsync(true);
        }

        private void CaptureLogo()
        {
            logo = corelService.CaptureSelection();
            logoAspect = logo.WidthMm / logo.HeightMm;
            LogoImage.Source = logo.Preview;
            LogoInfoText.Text = string.Format("{0} ta obyekt\nCorelda: {1} × {2} mm",
                logo.Count, Num.Format(Math.Round(logo.WidthMm, 1)), Num.Format(Math.Round(logo.HeightMm, 1)));

            // Bo'yi saqlanadi, eni logo proporsiyasi bo'yicha hisoblanadi
            if (Num.TryParse(LogoHeightBox.Text, out double height) && height > 0)
            {
                bool previous = suppressEvents;
                suppressEvents = true;
                LogoWidthBox.Text = Num.Format(Math.Round(height * logoAspect, 2));
                suppressEvents = previous;
            }
            RefreshPreview();
        }

        private static bool ConfirmWarnings(RuleResult rules)
        {
            string text = "Diqqat:\n\n" + string.Join("\n\n", rules.Warnings.Select(w => "• " + w)) +
                          "\n\nDizayn shu cheklovlar bilan chiziladi (yassi va/yoki yoritilmagan holda). Davom etilsinmi?";
            return MessageBox.Show(text, "Reklama Maket — ogohlantirish", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private bool RunCorelAction(Func<bool> action)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                return action();
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
            return false;
        }

        // ================================================================ ChatGPT

        private void LoadAiSettings()
        {
            ApiKeyBox.Password = appSettings.ApiKey;
            ModelCombo.ItemsSource = OpenAiImageClient.Models;
            ModelCombo.Text = string.IsNullOrWhiteSpace(appSettings.Model) ? OpenAiImageClient.Models[0] : appSettings.Model;
            QualityCombo.ItemsSource = OpenAiImageClient.Qualities;
            QualityCombo.SelectedItem = OpenAiImageClient.Qualities.Contains(appSettings.Quality) ? appSettings.Quality : "medium";
            SizeCombo.ItemsSource = OpenAiImageClient.Sizes;
            SizeCombo.SelectedItem = OpenAiImageClient.Sizes.Contains(appSettings.Size) ? appSettings.Size : "auto";
            PromptBox.Text = string.IsNullOrWhiteSpace(appSettings.Prompt) ? PromptBuilder.DefaultPrompt : appSettings.Prompt;
            AiDayCheck.IsChecked = appSettings.Day;
            AiNightCheck.IsChecked = appSettings.Night;
            AutoSendCheck.IsChecked = appSettings.AutoSend;
            KeyStatusText.Text = string.IsNullOrEmpty(appSettings.ApiKey) ? "Kalit kiritilmagan." : "Kalit saqlangan.";
        }

        private void SaveAiSettings()
        {
            try
            {
                appSettings.Model = string.IsNullOrWhiteSpace(ModelCombo.Text) ? OpenAiImageClient.Models[0] : ModelCombo.Text.Trim();
                appSettings.Quality = QualityCombo.SelectedItem as string ?? "medium";
                appSettings.Size = SizeCombo.SelectedItem as string ?? "auto";
                appSettings.Prompt = PromptBox.Text == PromptBuilder.DefaultPrompt ? null : PromptBox.Text;
                appSettings.Day = AiDayCheck.IsChecked == true;
                appSettings.Night = AiNightCheck.IsChecked == true;
                appSettings.AutoSend = AutoSendCheck.IsChecked == true;
                appSettings.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReklamaMaket: sozlamalarni saqlab bo'lmadi: " + ex);
            }
        }

        /// <summary>Saqlangan kalit; maydonga yangi kalit yozilgan bo'lsa — uni saqlab qaytaradi.</summary>
        private string CurrentApiKey()
        {
            string typed = ApiKeyBox.Password.Trim();
            if (typed.Length > 0 && typed != appSettings.ApiKey)
            {
                appSettings.ApiKey = typed;
                SaveAiSettings();
            }
            return appSettings.ApiKey;
        }

        private void SaveKeyButton_Click(object sender, RoutedEventArgs e)
        {
            appSettings.ApiKey = ApiKeyBox.Password;
            SaveAiSettings();
            KeyStatusText.Foreground = (Brush)FindResource("MutedBrush");
            KeyStatusText.Text = string.IsNullOrEmpty(appSettings.ApiKey) ? "Kalit o'chirildi." : "Kalit saqlandi (shu kompyuterda shifrlangan).";
        }

        private async void CheckKeyButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureSynchronizationContext();
            string key = CurrentApiKey();
            if (string.IsNullOrEmpty(key))
            {
                KeyStatusText.Foreground = (Brush)FindResource("ErrorBrush");
                KeyStatusText.Text = "Avval kalitni kiriting.";
                return;
            }

            CheckKeyButton.IsEnabled = false;
            KeyStatusText.Foreground = (Brush)FindResource("MutedBrush");
            KeyStatusText.Text = "Tekshirilmoqda...";
            try
            {
                using (var client = new OpenAiImageClient(key))
                    await client.CheckKeyAsync(CancellationToken.None);
                KeyStatusText.Foreground = (Brush)FindResource("OkBrush");
                KeyStatusText.Text = "✓ Kalit ishlayapti.";
            }
            catch (Exception ex)
            {
                KeyStatusText.Foreground = (Brush)FindResource("ErrorBrush");
                KeyStatusText.Text = ex is UserException ? ex.Message : "Xatolik: " + ex.Message;
            }
            finally
            {
                CheckKeyButton.IsEnabled = true;
            }
        }

        private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void PickPhotoButton_Click(object sender, RoutedEventArgs e) => PickPhoto();

        private void ClearPhotoButton_Click(object sender, RoutedEventArgs e)
        {
            photoPath = null;
            PhotoText.Text = "tanlanmagan";
            PhotoText.ToolTip = null;
        }

        private bool PickPhoto()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Joy rasmi (bino fasadi)",
                Filter = "Rasmlar (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp"
            };
            if (dialog.ShowDialog() != true)
                return false;

            if (new FileInfo(dialog.FileName).Length > MaxPhotoBytes)
            {
                SetAiStatus("Rasm 45 MB dan katta — kichikroq rasm tanlang.", true);
                return false;
            }

            photoPath = dialog.FileName;
            PhotoText.Text = Path.GetFileName(photoPath);
            PhotoText.ToolTip = photoPath;
            return true;
        }

        private void ResetPromptButton_Click(object sender, RoutedEventArgs e) => PromptBox.Text = PromptBuilder.DefaultPrompt;

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureSynchronizationContext();
            await GenerateAsync(false);
        }

        private void CancelAiButton_Click(object sender, RoutedEventArgs e) => aiCancel?.Cancel();

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (lastAiFolder != null && Directory.Exists(lastAiFolder))
                Process.Start("explorer.exe", "\"" + lastAiFolder + "\"");
        }

        /// <summary>
        /// Dizaynni Corel'dan PNG qilib oladi va ChatGPT'ga yuboradi: kunduzgi va kechki real ko'rinish.
        /// Natija papkaga saqlanadi va Corelga import qilinadi.
        /// </summary>
        private async Task GenerateAsync(bool automatic)
        {
            if (aiCancel != null)
                return; // allaqachon ishlayapti

            string key = CurrentApiKey();
            if (string.IsNullOrEmpty(key))
            {
                SetAiStatus("Avval OpenAI API kalitini kiriting va \"Saqlash\"ni bosing. Kalitni \"API kalit olish →\" havolasidan oling.", true);
                return;
            }

            bool day = AiDayCheck.IsChecked == true;
            bool night = AiNightCheck.IsChecked == true;
            if (!day && !night)
            {
                SetAiStatus("Kunduzi yoki kechasi — kamida bittasini belgilang.", true);
                return;
            }

            SignSettings settings = ReadSettings(out string error);
            if (settings == null)
            {
                SetAiStatus(error, true);
                return;
            }
            RuleResult rules = SignRules.Check(settings);

            if (!automatic && photoPath == null)
            {
                MessageBoxResult answer = MessageBox.Show(
                    "Dizaynni haqiqiy joy (bino fasadi) rasmida ko'rsatish uchun rasm tanlaysizmi?\n\n" +
                    "Ha — rasm tanlash\nYo'q — umumiy fasadda ko'rsatish",
                    "ChatGPT — joy rasmi", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Cancel || (answer == MessageBoxResult.Yes && !PickPhoto()))
                    return;
            }
            SaveAiSettings();

            var exported = new List<string>();
            string designFile;
            string nightFile = null;
            try
            {
                if (SourceSelectionRadio.IsChecked == true)
                {
                    designFile = corelService.ExportSelectionPng(AiImagePixels);
                }
                else
                {
                    if (lastResult?.DayView == null)
                        throw new UserException("Avval \"CORELDA DIZAYN CHIZISH\" tugmasini bosing (yoki \"Corelda tanlangan obyekt\"ni tanlang).");
                    designFile = corelService.ExportShapePng(lastResult.DayView, AiImagePixels);
                    if (night && lastResult.NightView != null)
                        nightFile = corelService.ExportShapePng(lastResult.NightView, AiImagePixels);
                }
                exported.Add(designFile);
                if (nightFile != null)
                    exported.Add(nightFile);
            }
            catch (Exception ex)
            {
                exported.ForEach(CorelService.TryDelete);
                SetAiStatus(ex is UserException ? ex.Message : "Dizaynni eksport qilib bo'lmadi: " + ex.Message, true);
                return;
            }

            string model = string.IsNullOrWhiteSpace(ModelCombo.Text) ? OpenAiImageClient.Models[0] : ModelCombo.Text.Trim();
            string quality = QualityCombo.SelectedItem as string ?? "medium";
            string size = SizeCombo.SelectedItem as string ?? "auto";
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ReklamaMaket", "ChatGPT", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

            aiCancel = new CancellationTokenSource();
            SetAiBusy(true);
            SetAiStatus("ChatGPT rasm yaratmoqda... odatda 1–2 daqiqa.", false);
            try
            {
                var errors = new List<string>();
                byte[] dayImage, nightImage;
                using (var client = new OpenAiImageClient(key))
                {
                    Task<byte[]> dayTask = null, nightTask = null;
                    if (day)
                    {
                        var images = new List<string> { designFile };
                        if (photoPath != null)
                            images.Add(photoPath);
                        string prompt = PromptBuilder.Build(settings, rules, PromptBox.Text, false, false, photoPath != null);
                        dayTask = client.EditImageAsync(images, prompt, model, size, quality, aiCancel.Token);
                    }
                    if (night)
                    {
                        var images = new List<string> { designFile };
                        if (nightFile != null)
                            images.Add(nightFile);
                        if (photoPath != null)
                            images.Add(photoPath);
                        string prompt = PromptBuilder.Build(settings, rules, PromptBox.Text, true, nightFile != null, photoPath != null);
                        nightTask = client.EditImageAsync(images, prompt, model, size, quality, aiCancel.Token);
                    }

                    dayImage = await Collect(dayTask, errors);
                    nightImage = await Collect(nightTask, errors);
                }

                var files = new List<string>();
                var labels = new List<string>();
                Directory.CreateDirectory(folder);
                AiDayImage.Source = Save(dayImage, Path.Combine(folder, "kunduzi.png"), "Kunduzi", files, labels);
                AiNightImage.Source = Save(nightImage, Path.Combine(folder, "kechasi.png"), "Kechasi", files, labels);

                if (files.Count == 0)
                {
                    SetAiStatus(string.Join("\n", errors.Distinct()), true);
                    return;
                }

                lastAiFolder = folder;
                AiResults.Visibility = Visibility.Visible;
                OpenFolderButton.Visibility = Visibility.Visible;

                string imported = string.Empty;
                try
                {
                    corelService.ImportImages(files, labels, settings.Letter.Name + " — ChatGPT ko'rinish", settings.NewPage);
                    imported = " va Corelga qo'shildi";
                }
                catch (Exception ex)
                {
                    errors.Add("Corelga import qilib bo'lmadi: " + ex.Message);
                }

                string message = "Tayyor: rasmlar saqlandi" + imported + ". Papka: " + folder;
                if (errors.Count > 0)
                    message += "\n" + string.Join("\n", errors.Distinct());
                SetAiStatus(message, errors.Count > 0);
            }
            catch (OperationCanceledException)
            {
                SetAiStatus("Bekor qilindi.", false);
            }
            catch (Exception ex)
            {
                SetAiStatus(ex is UserException ? ex.Message : "Xatolik: " + ex.Message, true);
            }
            finally
            {
                aiCancel.Dispose();
                aiCancel = null;
                SetAiBusy(false);
                exported.ForEach(CorelService.TryDelete);
            }
        }

        private static async Task<byte[]> Collect(Task<byte[]> task, List<string> errors)
        {
            if (task == null)
                return null;
            try
            {
                return await task;
            }
            catch (UserException ex)
            {
                errors.Add(ex.Message);
                return null;
            }
        }

        private static ImageSource Save(byte[] image, string file, string label, List<string> files, List<string> labels)
        {
            if (image == null)
                return null;
            File.WriteAllBytes(file, image);
            files.Add(file);
            labels.Add(label);
            return CorelService.LoadBitmap(file);
        }

        private void SetAiBusy(bool busy)
        {
            GenerateButton.IsEnabled = !busy;
            CancelAiButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            AiProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>await'dan keyin kod Corel/WPF oqimida davom etishi uchun.</summary>
        private void EnsureSynchronizationContext()
        {
            if (!(SynchronizationContext.Current is DispatcherSynchronizationContext))
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));
        }

        private void DockerUI_Unloaded(object sender, RoutedEventArgs e)
        {
            refreshTimer.Stop();
            SaveAiSettings();
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
