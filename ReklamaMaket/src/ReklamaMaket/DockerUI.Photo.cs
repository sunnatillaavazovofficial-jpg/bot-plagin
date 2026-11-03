using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ReklamaMaket.Core;
using ReklamaMaket.Rendering;

namespace ReklamaMaket
{
    /// <summary>
    /// "Joyida" bo'limi: bino rasmini tanlash (fayl, Ctrl+V yoki sudrab tashlash), logoni rasm ustida
    /// sichqoncha bilan surish va burchagidan tortib kattalashtirish. Corel'da va ChatGPT'da ham shu joylashuv ishlatiladi.
    /// </summary>
    public partial class DockerUI
    {
        private const int PhotoPreviewPixels = 1000;
        private static readonly string[] PhotoExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };

        private BitmapSource photoBitmap;
        private double photoAspect;
        private double photoX = 0.5;
        private double photoY = 0.45;

        // Hozir ko'rinib turgan rasm ustidagi ko'rinish (surish uchun)
        private PhotoPreview photoPreview;
        private SignSettings photoPreviewSettings;
        private bool draggingSign;
        private bool resizingSign;
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
            return dialog.ShowDialog() == true && SetPhoto(dialog.FileName);
        }

        /// <summary>Rasmni bino rasmi qilib qo'yadi (fayl yo'li Corel'ga import va ChatGPT uchun kerak).</summary>
        private bool SetPhoto(string path)
        {
            if (new FileInfo(path).Length > MaxPhotoBytes)
            {
                SetStatus("Rasm 45 MB dan katta — kichikroq rasm tanlang.", true);
                return false;
            }

            BitmapSource bitmap;
            try
            {
                bitmap = LoadPhoto(path);
            }
            catch (Exception ex)
            {
                SetStatus("Rasmni ochib bo'lmadi: " + ex.Message, true);
                return false;
            }

            photoPath = path;
            photoBitmap = bitmap;
            photoAspect = (double)bitmap.PixelWidth / Math.Max(bitmap.PixelHeight, 1);
            photoX = 0.5;
            photoY = 0.45;
            UpdatePhotoUi();
            PhotoViewToggle.IsChecked = true;
            SetStatus("Rasm qo'yildi. Logoni ko'rinishda suring.", false);
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
            PhotoPasteButton.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
            PhotoInfoPanel.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            PhotoThumb.ImageSource = photoBitmap;
            PhotoNameText.Text = has ? Path.GetFileName(photoPath) : string.Empty;
            PhotoNameText.ToolTip = photoPath;
            PhotoViewToggle.IsEnabled = has;
            if (!has)
                PhotoViewToggle.IsChecked = false;
            AiPhotoText.Text = has
                ? "Bino rasmi: «" + Path.GetFileName(photoPath) + "» — dizayn shu joyda ko'rsatiladi."
                : "Bino rasmi yo'q — dizayn umumiy binoda ko'rsatiladi («Joyida» bo'limida qo'shing).";
        }

        private void PhotoSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PhotoSizeText == null)
                return; // panel hali yaratilmoqda

            PhotoSizeText.Text = Math.Round(PhotoSizeSlider.Value) + "%";
            ScheduleRefresh();
        }

        // ---------------------------------------------------------------- Ctrl+V va sudrab tashlash

        private void DockerUI_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Faqat "Joyida" bo'limida: Ctrl+V — nusxalangan rasmni qo'yish (matn maydonlarida odatdagidek ishlaydi)
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && TabPhoto.IsChecked == true &&
                !(Keyboard.FocusedElement is TextBox))
            {
                PastePhoto();
                e.Handled = true;
            }
        }

        private void PhotoPaste_Click(object sender, RoutedEventArgs e) => PastePhoto();

        private void PastePhoto()
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    string file = ImageFiles(Clipboard.GetFileDropList()).FirstOrDefault();
                    if (file != null)
                    {
                        SetPhoto(file);
                        return;
                    }
                }
                if (Clipboard.ContainsImage())
                {
                    BitmapSource image = Clipboard.GetImage();
                    if (image != null)
                    {
                        SetPhoto(SaveTempPng(image));
                        return;
                    }
                }
                SetStatus("Buferda rasm yo'q. Rasmni nusxalang (Ctrl+C) yoki faylni shu yerga tashlang.", true);
            }
            catch (Exception ex)
            {
                SetStatus("Rasmni qo'yib bo'lmadi: " + ex.Message, true);
            }
        }

        private void Photo_DragOver(object sender, DragEventArgs e)
        {
            bool ok = e.Data.GetDataPresent(DataFormats.FileDrop) &&
                      ImageFiles((string[])e.Data.GetData(DataFormats.FileDrop)).Any();
            e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Photo_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            string file = ImageFiles((string[])e.Data.GetData(DataFormats.FileDrop)).FirstOrDefault();
            if (file != null)
            {
                TabPhoto.IsChecked = true;
                SetPhoto(file);
            }
            e.Handled = true;
        }

        private static string[] ImageFiles(StringCollection files) => ImageFiles(files.Cast<string>().ToArray());

        private static string[] ImageFiles(string[] files) =>
            (files ?? new string[0]).Where(f => PhotoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()) && File.Exists(f)).ToArray();

        /// <summary>Buferdagi rasm vaqtinchalik PNG faylga yoziladi (Corel'ga import va ChatGPT uchun).</summary>
        private static string SaveTempPng(BitmapSource image)
        {
            string file = Path.Combine(CorelService.TempFolder, "joy_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream stream = File.Create(file))
                encoder.Save(stream);
            return file;
        }

        // ---------------------------------------------------------------- tez joylash

        private void PhotoPlace_Click(object sender, RoutedEventArgs e)
        {
            photoX = 0.5;
            photoY = double.Parse((string)((FrameworkElement)sender).Tag, CultureInfo.InvariantCulture);
            PhotoViewToggle.IsChecked = true;
            ScheduleRefresh();
        }

        // ---------------------------------------------------------------- surish va kattalashtirish

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (photoPreview == null)
                return;

            PhotoPage.Focus(); // Ctrl+V panelga kelishi uchun
            Point point = e.GetPosition(photoPreview.Root);
            FrameworkElement sign = photoPreview.Sign;
            double left = Canvas.GetLeft(sign);
            double top = Canvas.GetTop(sign);

            // Burchakdagi doira — kattalashtirish
            if ((point - new Point(left + sign.Width, top + sign.Height)).Length <= photoPreview.HandleRadius * 1.6)
            {
                resizingSign = true;
            }
            else
            {
                bool onSign = point.X >= left && point.X <= left + sign.Width && point.Y >= top && point.Y <= top + sign.Height;
                // Belgidan ushlansa — ushlangan joyi bilan suriladi; bo'sh joy bosilsa — belgi o'sha yerga keladi
                dragOffset = onSign ? new Point(left + sign.Width / 2, top + sign.Height / 2) - point : new Vector();
                draggingSign = true;
                MoveSign(point);
            }
            PreviewHost.CaptureMouse();
            e.Handled = true;
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            if (photoPreview == null)
            {
                PreviewHost.Cursor = null;
                return;
            }

            Point point = e.GetPosition(photoPreview.Root);
            FrameworkElement sign = photoPreview.Sign;
            Point corner = new Point(Canvas.GetLeft(sign) + sign.Width, Canvas.GetTop(sign) + sign.Height);
            bool overHandle = (point - corner).Length <= photoPreview.HandleRadius * 1.6;
            PreviewHost.Cursor = resizingSign || overHandle ? Cursors.SizeNWSE : Cursors.SizeAll;

            if (draggingSign)
                MoveSign(point);
            else if (resizingSign)
                ResizeSign(point);
        }

        private void Preview_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!draggingSign && !resizingSign)
                return;

            draggingSign = false;
            resizingSign = false;
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
            PlaceSign(place);
        }

        /// <summary>Burchakdan tortish: chap-yuqori burchak joyida qoladi, eni sichqonchaga qarab o'zgaradi.</summary>
        private void ResizeSign(Point point)
        {
            FrameworkElement sign = photoPreview.Sign;
            double left = Canvas.GetLeft(sign), top = Canvas.GetTop(sign);
            double aspect = sign.Width / Math.Max(sign.Height, 1);
            // Diagonal bo'yicha: eni yoki bo'yidan qaysi biri kattaroq tortilgan bo'lsa
            double width = Math.Max(point.X - left, (point.Y - top) * aspect);
            double fraction = Num.Clamp(width / photoPreview.Width * 100, PhotoSizeSlider.Minimum, PhotoSizeSlider.Maximum);

            photoPreviewSettings.PhotoSignWidth = fraction / 100;
            PhotoPlacement place = PhotoLayout.Compute(photoPreviewSettings,
                (left + fraction / 100 * photoPreview.Width / 2) / photoPreview.Width,
                (top + fraction / 100 * photoPreview.Width / aspect / 2) / photoPreview.Height);
            photoX = place.CenterX;
            photoY = place.CenterY;
            PlaceSign(place);

            bool previous = suppressEvents;
            suppressEvents = true; // ko'rinish qo'yib yuborilganda yangilanadi
            PhotoSizeSlider.Value = fraction;
            PhotoSizeText.Text = Math.Round(fraction) + "%";
            suppressEvents = previous;
        }

        private void PlaceSign(PhotoPlacement place)
        {
            FrameworkElement sign = photoPreview.Sign;
            sign.Width = place.Width * photoPreview.Width;
            sign.Height = place.Height * photoPreview.Height;
            Canvas.SetLeft(sign, place.X * photoPreview.Width);
            Canvas.SetTop(sign, place.Y * photoPreview.Height);
            Canvas.SetLeft(photoPreview.Handle, place.X * photoPreview.Width + sign.Width - photoPreview.HandleRadius);
            Canvas.SetTop(photoPreview.Handle, place.Y * photoPreview.Height + sign.Height - photoPreview.HandleRadius);
        }
    }
}
