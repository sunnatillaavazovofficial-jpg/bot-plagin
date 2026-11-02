using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ReklamaMaket.Core;
using ReklamaMaket.Rendering;

namespace ReklamaMaket
{
    /// <summary>
    /// "Joyida" bo'limi: bino rasmini tanlash, logoni rasm ustida sichqoncha bilan surish (g'ildirak — kattalik).
    /// Corel'da va ChatGPT'da ham shu joylashuv ishlatiladi.
    /// </summary>
    public partial class DockerUI
    {
        private const int PhotoPreviewPixels = 1000;

        private BitmapSource photoBitmap;
        private double photoAspect;
        private double photoX = 0.5;
        private double photoY = 0.45;

        // Hozir ko'rinib turgan rasm ustidagi ko'rinish (surish uchun)
        private PhotoPreview photoPreview;
        private SignSettings photoPreviewSettings;
        private bool draggingSign;
        private Vector dragOffset;

        private bool PhotoMode => photoBitmap != null && PhotoViewToggle.IsChecked == true;

        private void PhotoPickButton_Click(object sender, RoutedEventArgs e) => PickPhoto();

        private void PhotoClearButton_Click(object sender, RoutedEventArgs e)
        {
            photoPath = null;
            photoBitmap = null;
            UpdatePhotoUi();
            ScheduleRefresh();
        }

        private bool PickPhoto()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Bino rasmi",
                Filter = "Rasmlar (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
            };
            if (dialog.ShowDialog() != true)
                return false;

            if (new FileInfo(dialog.FileName).Length > MaxPhotoBytes)
            {
                SetStatus("Rasm 45 MB dan katta — kichikroq rasm tanlang.", true);
                return false;
            }

            BitmapSource bitmap;
            try
            {
                bitmap = LoadPhoto(dialog.FileName);
            }
            catch (Exception ex)
            {
                SetStatus("Rasmni ochib bo'lmadi: " + ex.Message, true);
                return false;
            }

            photoPath = dialog.FileName;
            photoBitmap = bitmap;
            photoAspect = (double)bitmap.PixelWidth / Math.Max(bitmap.PixelHeight, 1);
            photoX = 0.5;
            photoY = 0.45;
            UpdatePhotoUi();
            PhotoViewToggle.IsChecked = true;
            SetStatus(string.Empty, false);
            ScheduleRefresh();
            return true;
        }

        private static BitmapSource LoadPhoto(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = PhotoPreviewPixels;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void UpdatePhotoUi()
        {
            bool has = photoBitmap != null;
            PhotoPickButton.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
            PhotoInfoPanel.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            PhotoThumb.ImageSource = photoBitmap;
            PhotoNameText.Text = has ? Path.GetFileName(photoPath) : string.Empty;
            PhotoNameText.ToolTip = photoPath;
            PhotoViewToggle.IsEnabled = has;
            if (!has)
                PhotoViewToggle.IsChecked = false;
            AiPhotoText.Text = has
                ? "Logo «" + Path.GetFileName(photoPath) + "» rasmida ko'rsatiladi."
                : "Bino rasmi yo'q — logo umumiy binoda ko'rsatiladi («Joyida» bo'limida tanlang).";
        }

        private void PhotoSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PhotoSizeText == null)
                return; // panel hali yaratilmoqda

            PhotoSizeText.Text = Math.Round(PhotoSizeSlider.Value) + "%";
            ScheduleRefresh();
        }

        // ---------------------------------------------------------------- surish va kattalashtirish

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (photoPreview == null)
                return;

            Point point = e.GetPosition(photoPreview.Root);
            FrameworkElement sign = photoPreview.Sign;
            double left = Canvas.GetLeft(sign);
            double top = Canvas.GetTop(sign);
            bool onSign = point.X >= left && point.X <= left + sign.Width && point.Y >= top && point.Y <= top + sign.Height;

            // Belgidan ushlansa — ushlangan joyi bilan suriladi; bo'sh joy bosilsa — belgi o'sha yerga keladi
            dragOffset = onSign ? new Point(left + sign.Width / 2, top + sign.Height / 2) - point : new Vector();
            draggingSign = true;
            PreviewHost.CaptureMouse();
            MoveSign(point);
            e.Handled = true;
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            PreviewHost.Cursor = photoPreview != null ? Cursors.SizeAll : null;
            if (draggingSign && photoPreview != null)
                MoveSign(e.GetPosition(photoPreview.Root));
        }

        private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!draggingSign)
                return;

            draggingSign = false;
            PreviewHost.ReleaseMouseCapture();
            ScheduleRefresh();
        }

        private void Preview_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (photoPreview == null)
                return;

            double factor = e.Delta > 0 ? 1.08 : 1 / 1.08;
            PhotoSizeSlider.Value = Num.Clamp(PhotoSizeSlider.Value * factor, PhotoSizeSlider.Minimum, PhotoSizeSlider.Maximum);
            e.Handled = true;
        }

        private void MoveSign(Point point)
        {
            Point target = point + dragOffset;
            PhotoPlacement place = PhotoLayout.Compute(photoPreviewSettings,
                target.X / photoPreview.Width, target.Y / photoPreview.Height);
            photoX = place.CenterX;
            photoY = place.CenterY;
            Canvas.SetLeft(photoPreview.Sign, place.X * photoPreview.Width);
            Canvas.SetTop(photoPreview.Sign, place.Y * photoPreview.Height);
        }
    }
}
