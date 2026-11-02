using System;
using System.Diagnostics;
using System.IO;

namespace ReklamaMaket.Core
{
    /// <summary>
    /// Asboblar panelidagi plagin tugmasiga rasm (icon.ico) qo'yadi. Corel buyruqlar paneli kechikkan bog'lanish
    /// (dynamic) orqali ishlatiladi: versiyada bunday imkoniyat bo'lmasa, jim o'tadi — plagin ishiga ta'sir qilmaydi.
    /// </summary>
    public static class CorelIcon
    {
        /// <summary>Tugma GUID'i (AppUI.xslt dagi itemData) va rasm fayli. Topilgan tugmalar soni qaytariladi.</summary>
        public static int TryApply(object app, string controlId, string iconFile)
        {
            if (app == null || string.IsNullOrEmpty(iconFile) || !File.Exists(iconFile))
                return 0;

            int applied = 0;
            try
            {
                dynamic bars = ((dynamic)app).CommandBars;
                int barCount = bars.Count;
                for (int i = 1; i <= barCount; i++)
                {
                    dynamic controls;
                    int controlCount;
                    try
                    {
                        controls = bars[i].Controls;
                        controlCount = controls.Count;
                    }
                    catch
                    {
                        continue;
                    }

                    for (int j = 1; j <= controlCount; j++)
                    {
                        try
                        {
                            dynamic control = controls[j];
                            string id = control.ID;
                            if (string.Equals((id ?? string.Empty).Trim('{', '}'), controlId, StringComparison.OrdinalIgnoreCase))
                            {
                                control.SetCustomIcon(iconFile);
                                applied++;
                            }
                        }
                        catch
                        {
                            // Ajratgich yoki boshqa turdagi element — o'tkazib yuboriladi
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Tugma rasmini qo'yib bo'lmadi: " + ex);
            }
            return applied;
        }
    }
}
