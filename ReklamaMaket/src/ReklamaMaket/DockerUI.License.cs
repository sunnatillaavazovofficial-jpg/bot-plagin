using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using ReklamaMaket.Licensing;

namespace ReklamaMaket
{
    /// <summary>
    /// Litsenziya: kalit bo'lmasa (yoki boshqa kompyuterniki / muddati tugagan bo'lsa) panel o'rniga
    /// faollashtirish oynasi ko'rinadi. Kalit %APPDATA%\ReklamaMaket\settings.json da saqlanadi.
    /// </summary>
    public partial class DockerUI
    {
        private LicenseState license = new LicenseState();

        private void InitLicense()
        {
            MachineCodeBox.Text = LicenseManager.MachineCode;
            LicenseSiteLink.NavigateUri = new Uri(LicenseManager.SiteUrl);
            license = LicenseManager.Check(appSettings.LicenseKey);
            if (!license.IsValid && !string.IsNullOrWhiteSpace(appSettings.LicenseKey))
            {
                // Saqlangan kalit endi yaroqsiz (masalan, muddati tugagan) — sababini ko'rsatamiz
                LicenseKeyBox.Text = appSettings.LicenseKey;
                SetLicenseStatus(license.Message, true);
            }
            ShowLicenseView(!license.IsValid, false);
        }

        /// <summary>Amal bajarishdan oldin: kalit yaroqli bo'lmasa, faollashtirish oynasi ochiladi.</summary>
        private bool RequireLicense()
        {
            license = LicenseManager.Check(appSettings.LicenseKey);
            if (license.IsValid)
                return true;

            SetLicenseStatus(license.Message, true);
            ShowLicenseView(true, false);
            return false;
        }

        private void ShowLicenseView(bool activation, bool canCancel)
        {
            MainView.Visibility = activation ? Visibility.Collapsed : Visibility.Visible;
            ActivationView.Visibility = activation ? Visibility.Visible : Visibility.Collapsed;
            CancelActivationButton.Visibility = canCancel ? Visibility.Visible : Visibility.Collapsed;
            LicenseInfoText.Text = license.IsValid ? license.Message : "Faollashtirilmagan";
        }

        private void SetLicenseStatus(string text, bool isError)
        {
            LicenseStatusText.Text = text;
            LicenseStatusText.Foreground = (Brush)FindResource(isError ? "ErrorBrush" : "OkBrush");
        }

        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            string key = LicenseKeyBox.Text.Trim();
            LicenseState state = LicenseManager.Check(key);
            if (!state.IsValid)
            {
                SetLicenseStatus(state.Message, true);
                return;
            }

            appSettings.LicenseKey = key;
            try
            {
                appSettings.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReklamaMaket: kalitni saqlab bo'lmadi: " + ex);
            }

            license = state;
            SetLicenseStatus(string.Empty, false);
            ShowLicenseView(false, false);
            SetStatus("✓ Faollashtirildi: " + state.Message, false);
        }

        private void CopyMachineCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(MachineCodeBox.Text);
                SetLicenseStatus("Kod nusxalandi — uni kalit olishda yuboring.", false);
            }
            catch (Exception)
            {
                // Bufer band bo'lsa — kodni qo'lda belgilab olish mumkin
                MachineCodeBox.Focus();
                MachineCodeBox.SelectAll();
            }
        }

        private void ChangeLicense_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;
            LicenseKeyBox.Text = string.Empty;
            SetLicenseStatus(string.Empty, false);
            ShowLicenseView(true, license.IsValid);
        }

        private void CancelActivation_Click(object sender, RoutedEventArgs e) => ShowLicenseView(!license.IsValid, false);
    }
}
