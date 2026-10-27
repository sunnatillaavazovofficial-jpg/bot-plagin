using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;
using ReklamaMaket.Model;
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
        private static readonly Color[] BackgroundPresets =
        {
            Colors.White,
            Color.FromRgb(0x1F, 0x23, 0x28),
            Color.FromRgb(0xE3, 0x06, 0x13),
            Color.FromRgb(0x00, 0x57, 0xB8),
            Color.FromRgb(0xFF, 0xD4, 0x00),
            Color.FromRgb(0x00, 0x96, 0x3F),
            Color.FromRgb(0xF2, 0x8C, 0x00),
            Color.FromRgb(0x6C, 0x2C, 0x91),
            Color.FromRgb(0x8A, 0x8D, 0x91),
        };

        private static readonly string[] ViewLabels = { "Chapdan", "Old tomondan", "O'ngdan" };
        private static readonly double[] ViewAngles = { -35, 0, 35 };

        private readonly corel.Application corelApp;
        private readonly CorelService corelService;
        private readonly DispatcherTimer refreshTimer;
        private readonly AxisAngleRotation3D yawRotation = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        private readonly AxisAngleRotation3D pitchRotation = new AxisAngleRotation3D(new Vector3D(1, 0, 0), 0);
        private readonly PerspectiveCamera camera = new PerspectiveCamera
        {
            LookDirection = new Vector3D(0, 0, -1),
            UpDirection = new Vector3D(0, 1, 0),
            FieldOfView = 40
        };

        private DesignType design;
        private LengthUnit unit = LengthUnit.Millimeter;
        private LogoInfo logo;
        private Color backgroundColor = Colors.White;
        private Color frameColor = Colors.Gray;
        private Scene3D scene;
        private bool suppressEvents = true;
        private bool showingValidationError;
        private Point? dragStart;
        private double zoom = 1.0;

        public DockerUI(object app)
        {
            InitializeComponent();

            corelApp = app as corel.Application;
            corelService = new CorelService(corelApp);

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            refreshTimer.Tick += (sender, e) =>
            {
                refreshTimer.Stop();
                RefreshPreview();
            };

            Setup3D();
            SetupLists();
            UpdateSliderTexts();

            suppressEvents = false;
            DesignCombo.SelectedIndex = 0;

            if (corelApp == null)
                SetStatus("CorelDRAW bilan bog'lanib bo'lmadi (VGCore).", true);
            else
                SetStatus("Corelda logoni belgilang va \"Tanlangan logoni olish\" tugmasini bosing.", false);
        }

        private TextBox[] SizeBoxes => new[] { WidthBox, HeightBox, DepthBox, ElevationBox };

        // ------------------------------------------------------------------ boshlang'ich sozlash

        private void Setup3D()
        {
            Viewport.Camera = camera;

            var rotation = new Transform3DGroup();
            rotation.Children.Add(new RotateTransform3D(yawRotation));
            rotation.Children.Add(new RotateTransform3D(pitchRotation));
            SceneVisual.Transform = rotation;

            yawRotation.Angle = -YawSlider.Value;
            pitchRotation.Angle = PitchSlider.Value;
        }

        private void SetupLists()
        {
            DesignCombo.ItemsSource = DesignCatalog.All;

            UnitCombo.ItemsSource = LengthUnit.All;
            UnitCombo.SelectedItem = unit;
            UpdateUnitTexts();

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

            var swatchStyle = (Style)FindResource("Swatch");
            foreach (Color color in BackgroundPresets)
            {
                var swatch = new Button
                {
                    Style = swatchStyle,
                    Background = new SolidColorBrush(color),
                    ToolTip = ToHex(color),
                    Tag = color
                };
                swatch.Click += BackgroundSwatch_Click;
                BackgroundSwatches.Children.Add(swatch);
            }

            UpdateColorControls();
        }

        private void ApplyDesign(DesignType newDesign)
        {
            design = newDesign;
            frameColor = newDesign.DefaultFrameColor;

            suppressEvents = true;
            try
            {
                DesignDescription.Text = newDesign.Description;
                PresetCombo.ItemsSource = newDesign.Presets;
                PresetCombo.SelectedIndex = 0;
                WriteSizes(newDesign.Presets[0]);
                UpdateColorControls();
            }
            finally
            {
                suppressEvents = false;
            }

            RefreshPreview();
        }

        // ------------------------------------------------------------------ sozlamalarni o'qish/yozish

        private void WriteSizes(SizePreset preset)
        {
            WidthBox.Text = Num.Format(unit.FromMm(preset.Width));
            HeightBox.Text = Num.Format(unit.FromMm(preset.Height));
            DepthBox.Text = Num.Format(unit.FromMm(preset.Depth));
            ElevationBox.Text = Num.Format(unit.FromMm(preset.Elevation));
            foreach (TextBox box in SizeBoxes)
                box.ClearValue(BorderBrushProperty);
        }

        private MockupSettings ReadSettings(out string error)
        {
            if (!ReadLength(WidthBox, "Eni", 50, out double width, out error) ||
                !ReadLength(HeightBox, "Bo'yi", 50, out double height, out error) ||
                !ReadLength(DepthBox, "Qalinligi", 1, out double depth, out error) ||
                !ReadLength(ElevationBox, "Yerdan balandligi", 0, out double elevation, out error))
            {
                return null;
            }

            var align = AlignCombo.SelectedItem as Choice<LogoAlign>;
            return new MockupSettings
            {
                Width = width,
                Height = height,
                Depth = depth,
                Elevation = elevation,
                MarginPercent = MarginSlider.Value,
                LogoSizePercent = LogoSizeSlider.Value,
                Align = align != null ? align.Value : LogoAlign.Center,
                Background = backgroundColor,
                FrameColor = frameColor,
                Scale = ScaleCombo.SelectedItem is int scale ? scale : 10,
                DrawEnvironment = EnvironmentCheck.IsChecked == true,
                DrawPerson = PersonCheck.IsChecked == true,
                DrawDimensions = DimensionsCheck.IsChecked == true,
                NewPage = NewPageCheck.IsChecked == true
            };
        }

        private bool ReadLength(TextBox box, string name, double minMm, out double mm, out string error)
        {
            mm = 0;
            error = null;

            if (!Num.TryParse(box.Text, out double value))
                error = name + ": son kiriting.";
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

        // ------------------------------------------------------------------ ko'rinishni yangilash

        private void ScheduleRefresh()
        {
            if (suppressEvents || refreshTimer == null)
                return;

            refreshTimer.Stop();
            refreshTimer.Start();
        }

        private void RefreshPreview()
        {
            if (design == null)
                return;

            MockupSettings settings = ReadSettings(out string error);
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

            try
            {
                SignModel model = SceneBuilder.Build(design, settings);
                BitmapSource face = FaceRenderer.Render(model, logo);
                FacePreview.Source = face;

                bool night = NightCheck.IsChecked == true;
                scene = Scene3DBuilder.Build(model, face, night);
                SceneVisual.Content = scene.Model;
                LightsVisual.Content = Scene3DBuilder.BuildLights(night);
                ViewHost.Background = SkyBrush(night);
                UpdateCamera();

                SizeInfoText.Text = string.Format("Bosma maydon (yuz): {0} × {1} mm",
                    Num.Format(Math.Round(model.Face.W)), Num.Format(Math.Round(model.Face.H)));
            }
            catch (Exception ex)
            {
                SetStatus("Ko'rinishni chizishda xato: " + ex.Message, true);
            }

            UpdateSliderTexts();
        }

        private void UpdateCamera()
        {
            if (scene == null)
                return;

            double aspect = ViewHost.ActualWidth > 1 && ViewHost.ActualHeight > 1
                ? ViewHost.ActualWidth / ViewHost.ActualHeight
                : 1.5;
            double tan = Math.Tan(camera.FieldOfView * Math.PI / 360);

            // Burilganda chuqurlik ham eniga qo'shiladi
            double radius = Math.Sqrt(scene.HalfWidth * scene.HalfWidth + scene.HalfDepth * scene.HalfDepth);
            double distance = Math.Max(radius / tan, scene.HalfHeight * aspect / tan) * 1.15 + scene.HalfDepth;
            camera.Position = new Point3D(0, 0, distance * zoom);
        }

        private static Brush SkyBrush(bool night)
        {
            var brush = night
                ? new LinearGradientBrush(Color.FromRgb(0x0B, 0x14, 0x26), Color.FromRgb(0x22, 0x2E, 0x48), 90)
                : new LinearGradientBrush(Color.FromRgb(0x8E, 0xC1, 0xEA), Color.FromRgb(0xEE, 0xF6, 0xFC), 90);
            brush.Freeze();
            return brush;
        }

        private void UpdateSliderTexts()
        {
            // InitializeComponent vaqtida slayderlar hodisa chiqaradi, bu matnlar hali yaratilmagan bo'ladi
            if (LogoSizeValueText == null)
                return;

            YawValueText.Text = Math.Round(YawSlider.Value) + "°";
            PitchValueText.Text = Math.Round(PitchSlider.Value) + "°";
            MarginValueText.Text = Math.Round(MarginSlider.Value) + "%";
            LogoSizeValueText.Text = Math.Round(LogoSizeSlider.Value) + "%";
        }

        private void UpdateUnitTexts()
        {
            WidthUnitText.Text = unit.Name;
            HeightUnitText.Text = unit.Name;
            DepthUnitText.Text = unit.Name;
            ElevationUnitText.Text = unit.Name;
        }

        private void UpdateColorControls()
        {
            bool previous = suppressEvents;
            suppressEvents = true;
            try
            {
                BackgroundButton.Background = new SolidColorBrush(backgroundColor);
                FrameButton.Background = new SolidColorBrush(frameColor);
                BackgroundHex.Text = ToHex(backgroundColor);
                FrameHex.Text = ToHex(frameColor);
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

        // ------------------------------------------------------------------ hodisalar: sozlamalar

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            if (suppressEvents)
                return;

            UpdateSliderTexts();
            ScheduleRefresh();
        }

        private void Size_Changed(object sender, RoutedEventArgs e)
        {
            if (suppressEvents)
                return;

            // Qo'lda o'zgartirildi — tayyor o'lcham endi mos emas
            suppressEvents = true;
            PresetCombo.SelectedIndex = -1;
            suppressEvents = false;

            ScheduleRefresh();
        }

        private void DesignCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressEvents)
                return;

            if (DesignCombo.SelectedItem is DesignType selected)
                ApplyDesign(selected);
        }

        private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressEvents || !(PresetCombo.SelectedItem is SizePreset preset))
                return;

            suppressEvents = true;
            try
            {
                WriteSizes(preset);
            }
            finally
            {
                suppressEvents = false;
            }
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
                foreach (TextBox box in SizeBoxes)
                {
                    if (Num.TryParse(box.Text, out double value))
                        box.Text = Num.Format(newUnit.FromMm(oldUnit.ToMm(value)));
                }
                UpdateUnitTexts();
            }
            finally
            {
                suppressEvents = false;
            }
            ScheduleRefresh();
        }

        // ------------------------------------------------------------------ hodisalar: ranglar

        private void BackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            if (PickColor(ref backgroundColor))
            {
                UpdateColorControls();
                ScheduleRefresh();
            }
        }

        private void FrameButton_Click(object sender, RoutedEventArgs e)
        {
            if (PickColor(ref frameColor))
            {
                UpdateColorControls();
                ScheduleRefresh();
            }
        }

        private void BackgroundSwatch_Click(object sender, RoutedEventArgs e)
        {
            backgroundColor = (Color)((Button)sender).Tag;
            UpdateColorControls();
            ScheduleRefresh();
        }

        private void BackgroundHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents || !TryParseHex(BackgroundHex.Text, out Color color))
                return;

            backgroundColor = color;
            BackgroundButton.Background = new SolidColorBrush(color);
            ScheduleRefresh();
        }

        private void FrameHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (suppressEvents || !TryParseHex(FrameHex.Text, out Color color))
                return;

            frameColor = color;
            FrameButton.Background = new SolidColorBrush(color);
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

        private static string ToHex(Color color) => string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);

        private static bool TryParseHex(string text, out Color color)
        {
            color = Colors.White;
            string hex = (text ?? string.Empty).Trim().TrimStart('#');
            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
                return false;

            color = Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
            return true;
        }

        // ------------------------------------------------------------------ hodisalar: 3D ko'rinish

        private void YawSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Slayder chapga — konstruksiyaga chap tomondan qaraymiz
            yawRotation.Angle = -e.NewValue;
            UpdateSliderTexts();
        }

        private void PitchSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            pitchRotation.Angle = e.NewValue;
            UpdateSliderTexts();
        }

        private void ViewLeft_Click(object sender, RoutedEventArgs e) => YawSlider.Value = ViewAngles[0];

        private void ViewFront_Click(object sender, RoutedEventArgs e) => YawSlider.Value = ViewAngles[1];

        private void ViewRight_Click(object sender, RoutedEventArgs e) => YawSlider.Value = ViewAngles[2];

        private void ViewHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStart = e.GetPosition(ViewHost);
            ViewHost.CaptureMouse();
        }

        private void ViewHost_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragStart == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point position = e.GetPosition(ViewHost);
            Vector delta = position - dragStart.Value;
            dragStart = position;

            YawSlider.Value -= delta.X * 0.5;
            PitchSlider.Value += delta.Y * 0.3;
        }

        private void ViewHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            dragStart = null;
            ViewHost.ReleaseMouseCapture();
        }

        private void ViewHost_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            zoom = Num.Clamp(zoom * (e.Delta > 0 ? 0.9 : 1.1), 0.35, 3);
            UpdateCamera();
            e.Handled = true;
        }

        private void ViewHost_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCamera();

        // ------------------------------------------------------------------ hodisalar: Corel

        private void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            RunCorelAction(() =>
            {
                CaptureLogo();
                SetStatus("Logo olindi. Endi dizayn turi va o'lchamlarni tanlang.", false);
            });
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            RunCorelAction(() =>
            {
                corel.Document doc = corelService.RequireDocument();
                MockupSettings settings = ReadSettings(out string error);
                if (settings == null)
                    throw new UserException(error);

                if (logo == null)
                {
                    if (!corelService.HasSelection())
                        throw new UserException("Avval Corelda logoni belgilang va \"Tanlangan logoni olish\" tugmasini bosing.");
                    CaptureLogo();
                }

                SignModel model = SceneBuilder.Build(design, settings);
                new CorelSceneRenderer(corelApp).Draw(doc, model, logo);

                SetStatus(string.Format("Tayyor: {0} maketi {1} yaratildi. Bekor qilish — Ctrl+Z.",
                    design.Name, settings.NewPage ? "yangi sahifada" : "joriy sahifada"), false);
            });
        }

        private void ExportViewsButton_Click(object sender, RoutedEventArgs e)
        {
            var files = new List<string>();
            RunCorelAction(() =>
            {
                corel.Document doc = corelService.RequireDocument();
                if (scene == null)
                    throw new UserException("Avval to'g'ri o'lchamlarni kiriting.");

                ViewTabs.SelectedIndex = 0;
                ViewTabs.UpdateLayout();
                double savedYaw = YawSlider.Value;
                try
                {
                    foreach (double angle in ViewAngles)
                    {
                        YawSlider.Value = angle;
                        files.Add(SaveViewImage());
                    }
                }
                finally
                {
                    YawSlider.Value = savedYaw;
                }

                double aspect = ViewHost.ActualWidth / Math.Max(1, ViewHost.ActualHeight);
                new CorelSceneRenderer(corelApp).ImportViews(doc, files, ViewLabels, aspect,
                    NewPageCheck.IsChecked == true, design.Name + " — 3D ko'rinish");

                SetStatus("3D ko'rinishlar (chapdan, old, o'ngdan) Corelga qo'shildi.", false);
            });

            foreach (string file in files)
                CorelService.TryDelete(file);
        }

        private void CaptureLogo()
        {
            logo = corelService.CaptureSelection();
            LogoImage.Source = logo.Preview;
            LogoInfoText.Text = string.Format("{0} ta obyekt\n{1} × {2} mm",
                logo.Count, Num.Format(Math.Round(logo.WidthMm, 1)), Num.Format(Math.Round(logo.HeightMm, 1)));
            RefreshPreview();
        }

        /// <summary>3D oynani 3 barobar katta aniqlikda PNG faylga saqlaydi.</summary>
        private string SaveViewImage()
        {
            const double factor = 3;
            ViewHost.UpdateLayout();
            double width = Math.Max(1, ViewHost.ActualWidth);
            double height = Math.Max(1, ViewHost.ActualHeight);

            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
                dc.DrawRectangle(new VisualBrush(ViewHost), null, new Rect(0, 0, width, height));

            var bitmap = new RenderTargetBitmap((int)(width * factor), (int)(height * factor), 96 * factor, 96 * factor, PixelFormats.Pbgra32);
            bitmap.Render(visual);

            string file = Path.Combine(CorelService.TempFolder, "view_" + Guid.NewGuid().ToString("N") + ".png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(file))
                encoder.Save(stream);
            return file;
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

        private void DockerUI_Unloaded(object sender, RoutedEventArgs e) => refreshTimer.Stop();

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
