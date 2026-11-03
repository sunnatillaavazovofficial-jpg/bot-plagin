using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace ReklamaMaket.Ui
{
    /// <summary>
    /// Pipetka: butun ekranning surati olinadi va ustida ochiladi — istalgan joyni (Corel, rasm, brauzer)
    /// bosib rangini olish mumkin. Kattalashtiruvchi oyna piksellarni ko'rsatadi. Esc yoki o'ng tugma — bekor qilish.
    /// </summary>
    public static class EyeDropper
    {
        public static Color? Pick()
        {
            Drawing.Rectangle area = Forms.SystemInformation.VirtualScreen;
            BitmapSource shot;
            using (var bitmap = new Drawing.Bitmap(area.Width, area.Height, Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Drawing.Graphics g = Drawing.Graphics.FromImage(bitmap))
                    g.CopyFromScreen(area.Left, area.Top, 0, 0, area.Size);

                IntPtr handle = bitmap.GetHbitmap();
                try
                {
                    shot = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                }
                finally
                {
                    DeleteObject(handle);
                }
            }
            var bgra = new FormatConvertedBitmap(shot, PixelFormats.Bgra32, null, 0);
            bgra.Freeze();

            var window = new PickerWindow(bgra);
            window.ShowDialog();
            return window.Picked;
        }

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        private sealed class PickerWindow : Window
        {
            private const int LensPixels = 11;
            private const double LensSize = 132;

            private readonly BitmapSource shot;
            private readonly byte[] pixels;
            private readonly Border lens;
            private readonly ImageBrush zoom;
            private readonly Rectangle swatch;
            private readonly TextBlock hex;
            private readonly Canvas canvas;

            public PickerWindow(BitmapSource shot)
            {
                this.shot = shot;
                pixels = new byte[shot.PixelWidth * shot.PixelHeight * 4];
                shot.CopyPixels(pixels, shot.PixelWidth * 4, 0);

                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Topmost = true;
                Left = SystemParameters.VirtualScreenLeft;
                Top = SystemParameters.VirtualScreenTop;
                Width = SystemParameters.VirtualScreenWidth;
                Height = SystemParameters.VirtualScreenHeight;
                Cursor = Cursors.Cross;

                zoom = new ImageBrush(shot) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
                RenderOptions.SetBitmapScalingMode(zoom, BitmapScalingMode.NearestNeighbor);
                swatch = new Rectangle { Width = 22, Height = 22, RadiusX = 6, RadiusY = 6, Stroke = Brushes.White, StrokeThickness = 1.5 };
                hex = new TextBlock { Foreground = Brushes.White, FontFamily = new FontFamily("Consolas"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };

                var lensGrid = new Grid();
                lensGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(LensSize) });
                lensGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var view = new Grid { Width = LensSize, Height = LensSize, ClipToBounds = true };
                view.Children.Add(new Rectangle { Fill = zoom });
                double cell = LensSize / LensPixels;
                view.Children.Add(new Rectangle
                {
                    Width = cell, Height = cell, Stroke = Brushes.White, StrokeThickness = 1.5,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                });
                lensGrid.Children.Add(view);
                var info = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 6, 8, 6) };
                info.Children.Add(swatch);
                info.Children.Add(hex);
                Grid.SetRow(info, 1);
                lensGrid.Children.Add(info);

                lens = new Border
                {
                    Child = lensGrid,
                    Background = new SolidColorBrush(Color.FromRgb(0x11, 0x13, 0x18)),
                    CornerRadius = new CornerRadius(12),
                    BorderBrush = Brushes.White,
                    BorderThickness = new Thickness(2),
                    IsHitTestVisible = false
                };

                canvas = new Canvas();
                canvas.Children.Add(lens);
                var root = new Grid();
                root.Children.Add(new Image { Source = shot, Stretch = Stretch.Fill });
                root.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x11, 0x13, 0x18)),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(14, 8, 14, 8),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 24, 0, 0),
                    IsHitTestVisible = false,
                    Child = new TextBlock { Foreground = Brushes.White, FontSize = 14, Text = "Rangni olish uchun bosing · Esc — bekor qilish" }
                });
                root.Children.Add(canvas);
                Content = root;

                MouseMove += (s, e) => Track(e.GetPosition(this));
                MouseLeftButtonUp += (s, e) =>
                {
                    Picked = ColorAt(e.GetPosition(this), out _, out _);
                    Close();
                };
                MouseRightButtonUp += (s, e) => Close();
                KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Escape)
                        Close();
                };
                Loaded += (s, e) =>
                {
                    Activate();
                    Focus();
                    Track(Mouse.GetPosition(this));
                };
            }

            public Color? Picked { get; private set; }

            private Color ColorAt(Point p, out int x, out int y)
            {
                x = (int)Math.Max(0, Math.Min(shot.PixelWidth - 1, p.X / Math.Max(ActualWidth, 1) * shot.PixelWidth));
                y = (int)Math.Max(0, Math.Min(shot.PixelHeight - 1, p.Y / Math.Max(ActualHeight, 1) * shot.PixelHeight));
                int i = (y * shot.PixelWidth + x) * 4;
                return Color.FromRgb(pixels[i + 2], pixels[i + 1], pixels[i]);
            }

            private void Track(Point p)
            {
                Color color = ColorAt(p, out int x, out int y);
                zoom.Viewbox = new Rect(x - LensPixels / 2, y - LensPixels / 2, LensPixels, LensPixels);
                swatch.Fill = new SolidColorBrush(color);
                hex.Text = string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);

                // Oyna kursorning yonida, ekran chetidan chiqmaydi
                double left = p.X + 24, top = p.Y + 24;
                if (left + LensSize + 10 > ActualWidth)
                    left = p.X - LensSize - 28;
                if (top + LensSize + 50 > ActualHeight)
                    top = p.Y - LensSize - 60;
                Canvas.SetLeft(lens, left);
                Canvas.SetTop(lens, top);
            }
        }
    }
}
