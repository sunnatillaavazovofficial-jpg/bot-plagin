# Reklama Rasxod — CorelDRAW plagini (material rasxodi va smeta)

Alohida CorelDRAW paneli: tanlangan logo va dizayn bo'yicha **qancha material ketishini** hisoblaydi —
akril, fomaks, alyukobond, metall list, orakal, diod (LED), blok pitaniya, sim, temir (profil), kraska,
kley, germetik, samorez, dyubel va dizayndan kelib chiqadigan **qo'shimcha materiallar** (bort lentasi, kant,
distansiya, gofra, nur tarqatgich, setka, lampochka, shpilka, zaklepka, lazer/frezer kesish...).
Har bir materialga **narxni qo'lda kiritasiz** — plagin uni eslab qoladi va smetani jamlaydi.
Dizayn panelning tepasida doim ko'rinib turadi (kunduzi / kechasi).

Dizaynlar manbasi — [Reklama Maket](../ReklamaMaket/): 21 ta logo dizayni (hajmli harflar va fonga o'yilgan logo),
11 ta fon, 30 ta gul, detallar va qoidalar bir xil (kod umumiy). Reklama Maket'da yangi dizayn qo'shilsa,
Rasxod'da ham avtomatik paydo bo'ladi.

## Ish tartibi

1. Corelda logoni belgilang → **Tanlangan logoni olish**. Plagin logoni yuqori aniqlikda o'lchaydi:
   harflar yuzi (m²), kontur uzunligi (bort, kant va kesish uchun), harflar (bo'laklar) soni.
   Yoki **Reklama Maket'dagi oxirgi dizaynni olish** — Reklama Maket'da chizilgan dizayn (harf, fon, o'lchamlar,
   ranglar, detallar va logo) bir bosishda o'tadi.
2. Logoning haqiqiy **bo'yi**ni kiriting (eni proporsiya bo'yicha o'zi chiqadi).
3. **Logo dizayni** va **fon dizayni**ni tanlang. Fon bo'lsa — o'lchami (avtomatik yoki qo'lda), qalinligi,
   gul, yoritilishi. Fonsiz bo'lsa — "Fonsiz (to'g'ridan-to'g'ri devorga)".
4. Kerak bo'lsa **Konstruksiya** (harf borti, orqasi, fomaks/alyukobond qalinligi, karkas profili) va
   **Detallar** (distansiya, LED nuqta, ramka, reyka...) ni o'zgartiring.
5. **Rasxod** jadvalida har bir materialga narx kiriting (bir marta — keyingi safar eslab qoladi).
   Ish haqi, montaj, transport kabi xarajatlarni **qo'shimcha qator** qilib qo'shing, **ustama (%)** bering.
   Pastda **JAMI** summa ko'rinadi.
6. **Excel (CSV) saqlash** — smeta faylga; **COREL'GA SMETA** — jadval Corel sahifasiga, logoning yoniga
   (bitta guruh, `Ctrl+Z` bilan bekor qilinadi).

## Nima hisoblanadi

Hisob har qatorda **qayerga ketgani** bilan ko'rsatiladi (masalan: *harf yuzi 0.65 m²; fon paneli 2.38 m²*).
Bir xil material (masalan fomaks — harf orqasiga ham, laytboksga ham) bitta qatorga yig'iladi.

| Guruh | Materiallar | Qanday hisoblanadi |
|---|---|---|
| List va plyonka | akril (sutsimon, shaffof, rangli; qalinligi bo'yicha), fomaks, alyukobond, metall list, setka, orakal (translucent / oddiy) | yuza × (1 + chiqindi %); izohda list soni yoki orakal pogon metri |
| Temir, profil, bort | alyuminiy bort lentasi (eni = harf qalinligi), kant (trimcap), profil quvur (karkas), laytboks profili, yog'och brus, elektrod | kontur uzunligi yoki karkas uzunligi (+5%); izohda necha dona 6 m |
| Elektr | LED modul, ochiq LED / piksel, lampochka + patron, LED / neon lenta, blok pitaniya, germetik quti, sim 2×0,75, kabel 3×1,5, gofra, klemma | quyida |
| Mahkamlash | samorez (harf yig'ish, panelga), dyubel-samorez (devorga), distansiya, dekor bolt, shpilka, zaklepka | qadam va yuza bo'yicha |
| Kley, kraska | kley (flakon), germetik (tuba), kraska (emal), gruntovka, lak, epoksid, ikki tomonlama skotch | chok uzunligi / bo'yaladigan yuza bo'yicha |
| Ishlov berish | lazer kesish, frezer kesish (CNC), gravirovka | kesiladigan kontur uzunligi, m |

**LED modullar.** Modullar har bir chiziq (shtrix) bo'ylab qadam bilan qo'yiladi: *qadam = qalinlik × 1,5*
(50–250 mm), keng joyda bir necha qator; har bir harfga kamida bitta. Laytboks va yonadigan fon orqasida —
to'r bo'yicha (qadam kamida 100 mm), fonning qaysi joyi yonishiga qarab (butunlay, logo atrofi; chetlari va
pastdan yonsa — LED lenta). Ochiq LED, lampochka va perforatsiya soni — Corel chizmasidagi to'r bilan bir xil.

**Blok pitaniya** = jami quvvat × (1 + zaxira 20%) / blok quvvati (200 W), yuqoriga yaxlitlanadi.

**Konstruksiya dizayn turiga qarab tanlanadi** (Konstruksiya bo'limida o'zgartirsa bo'ladi):

| Logo dizayni | Yuz | Bort | Orqa | Yoritish |
|---|---|---|---|---|
| Old tomoni yonadigan, oq yuz/qora bort, qiya qirrali | sutsimon akril 3 mm (qiya — 8 mm), rangli bo'lsa + orakal | alyuminiy lenta | fomaks | LED modul |
| Old + orqa yonadigan | sutsimon akril | alyuminiy lenta | shaffof akril | LED modul (+25%), distansiya |
| Orqadan yonadigan (kontrajur) | alyukobond | alyuminiy lenta | shaffof akril | LED modul, distansiya |
| Yon tomoni (qisman) yonadi | alyukobond yoki akril | alyuminiy lenta + akril chiziq | fomaks | LED modul |
| Yon tomoni to'liq / hamma tomoni yonadi | alyukobond yoki akril | sutsimon akril tasma | fomaks | LED modul |
| Yaxlit akril (yumaloq / rangli) | akril, qalinligi = harf qalinligi (50 mm dan qalin — qatlamlar) | — | fomaks 3 mm qopqoq | LED modul |
| Ochiq LED (skelet) | fomaks (oq plastik) | alyuminiy lenta | fomaks | ochiq LED nuqtalar |
| Lampochkali (marquee) | metall list, bo'yalgan | metall list | fomaks | lampochka + patron (220V) |
| Perforatsiyali + cheti yonadi | metall list + perforatsiya (lazer) | metall list | fomaks | piksel LED + neon lenta |
| Naqshli lazer kesma yuz | metall list + naqsh (lazer) + akril qatlam | metall list | fomaks | LED modul |
| Akril harf, yoritilmaydi | shaffof akril (qalinligi = harf qalinligi) + orakal | — | — | — (distansiyada) |
| Metall harf, yoritilmaydi | metall list, bo'yalgan | metall list | ochiq | — (shpilka + epoksid) |
| O'yilgan logo (alyukobond) | o'yiqqa akril / chiqib turuvchi akril / orqa panel / gravirovka | — | — | quti ichida LED modul |

Logo **20 sm dan past** bo'lsa — yassi harf (fomaks, akril yoki alyukobond + orakal, skotch bilan), yoritilmaydi; **qalinligi 4 sm dan kam** —
LED hisoblanmaydi (Reklama Maket qoidalari).

**Fonlar:** kompozit panel va kasseta (alyukobond + profil karkas + samorez + dyubel + kraska), laytboks
(akril yuz, alyuminiy profil, orqa devor, ichki ustunlar, LED), akril plita (dekor boltlar), alyukobond quti
(yuz + bort + orqa, karkas, zaklepka, germetik), gulli list (metall list, teshiklarni lazer kesish, bo'yash),
ramkadagi / yog'och ramkali setka, gorizontal va vertikal reykalar (profil, ustunlar, LED lenta oraliqda).
Fon yonsa — orqa quti, nur tarqatgich (diffuzor) va LED qo'shiladi.

## Narx, miqdor va smeta

- **Narx** har bir material uchun bir marta kiritiladi va `%APPDATA%\ReklamaRasxod\settings.json` da saqlanadi.
  Narx kaliti — material + birlik (m² narxi va butun list narxi alohida).
- **Miqdor**ni qo'lda o'zgartirish mumkin (qalin yozuv bilan ko'rinadi, ↺ — hisoblanganiga qaytaradi).
  `0` — material smetaga kirmaydi. Logo yoki fon dizayni almashtirilsa, qo'lda kiritilgan miqdorlar tozalanadi.
- **Listlar**: *m² bo'yicha* (izohda ≈ list soni) yoki *butun list bo'yicha* (narx — 1 list uchun).
- **Qo'shimcha xarajatlar**: ish haqi, montaj, transport, dizayn... — nomi, miqdori, birligi, narxi (saqlanadi).
- **Ustama (foyda)**, **valyuta** nomi (standart — so'm).
- Narxi kiritilmagan materiallar jamiga qo'shilmaydi — pastda nechtasi qolgani yoziladi.

## Sarf normalari

**7. Sarf normalari** bo'limida hisob normalarini o'zgartirish mumkin (saqlanadi; "Standart normalarga qaytarish"):
listlar chiqindisi (25%), orakal chiqindisi (20%), list o'lchamlari (akril 2050×3050, fomaks va alyukobond 1220×2440,
metall 1250×2500), orakal rulon eni, profil uzunligi (6 m), LED modul quvvati (0,72 W), modul qadami koeffitsiyenti,
LED lenta quvvati, blok quvvati va zaxirasi, sim va kabel uzunligi, mahkamlash nuqtalari, samorez va dyubel qadami,
karkas ustunlari oralig'i, kley va germetik sarfi, kraska sarfi va qavatlari, gruntovka.

## Reklama Maket bilan bog'lanish

Reklama Maket har safar **CORELDA DIZAYN CHIZISH** bosilganda dizaynni `%APPDATA%\ReklamaMaket\last-design.json`
va logo rasmini `last-logo.png` ga saqlaydi. Rasxod panelidagi **Reklama Maket'dagi oxirgi dizaynni olish** tugmasi
shuni ochadi. (Bu imkoniyat uchun Reklama Maket'ni ham shu versiyaga qayta yig'ing.)

## O'rnatish

Talablar: Windows 10/11, CorelDRAW X7 yoki yangisi (64-bit), hamda bittasi:
[.NET SDK](https://dotnet.microsoft.com/download) **yoki** Visual Studio 2022 (".NET desktop development").
`ReklamaRasxod` papkasi `ReklamaMaket` papkasi bilan yonma-yon turishi kerak (umumiy kod shu yerdan olinadi).

1. CorelDRAW'ni yoping.
2. `ReklamaRasxod\build.bat` ni ikki marta bosing — Corel'ni topadi, plaginni yig'adi va
   `...\Programs64\Addons\ReklamaRasxod\` papkasiga nusxalaydi (administrator ruxsatini so'raydi).
3. CorelDRAW'ni oching: **Rasxod → Reklama Rasxod** menyusi yoki standart asboblar panelidagi tugma.
   Ko'rinmasa — CorelDRAW'ni **F8** bosib turgan holda ishga tushiring.

Qo'shimcha: `build.bat -CorelPath "C:\...\Programs64"`, `build.bat -NoInstall`, `build.bat -Uninstall`.
Ikkala plagin (Maket va Rasxod) bir vaqtda o'rnatilishi mumkin — alohida menyu va panel.

## Loyiha tuzilishi

```
ReklamaRasxod/
├── build.bat, build.ps1              yig'ish va o'rnatish
└── src/ReklamaRasxod/
    ├── AppUI.xslt, UserUI.xslt       Corel paneli, "Rasxod" menyusi va tugma
    ├── DockerUI.xaml(.cs)            panel: dizayn tanlash, ko'rinish, logo olish, Reklama Maket dizaynini olish
    ├── DockerUI.Details.cs           "Detallar" bo'limi (Reklama Maket bilan bir xil)
    ├── DockerUI.Estimate.cs          rasxod jadvali, narxlar, qo'shimcha qatorlar, jami, normalar, eksport
    ├── Styles/Theme.xaml             panel ranglari
    ├── Core/LogoShape.cs             logoni o'lchash: yuza, kontur, bo'laklar, ichiga sig'adigan nuqtalar
    ├── Core/LogoCapture.cs           Corel'dan logoni olish (PNG eksport) yoki rasmdan
    ├── Core/RasxodSettings.cs        narxlar, normalar, tanlovlar — settings.json
    ├── Estimate/Estimator.cs         dizayn → material qatorlari (harflar, o'yilgan logo, fon, elektr, mahkamlash)
    ├── Estimate/Construction.cs      harf turi qanday yasaladi (yuz, bort, orqa, LED)
    ├── Estimate/Norms.cs             sarf normalari
    ├── Estimate/MaterialLine.cs      jadval qatori va guruhlar
    └── Export/EstimateExport.cs      Excel (CSV) va Corel jadvali
```

Reklama Maket'dan umumiy fayllar (`Designs/*`, `Core/SignSettings`, `SignRules`, `SignDetails`, `LogoLayout`,
`PatternGeometry`, `DesignSnapshot`..., `Rendering/PreviewBuilder`) loyihaga havola (link) sifatida qo'shilgan.

## Yangi material yoki dizayn

- Yangi **logo / fon dizayni** Reklama Maket'ning `Designs/` ro'yxatiga qo'shiladi — Rasxod uni avtomatik oladi;
  konstruksiya yorug'lik zonalari va yuz materialiga qarab tanlanadi (`Estimate/Construction.cs`).
- Yangi **material qatori** — `Estimate/Estimator.cs` dagi tegishli joyga bitta `Add(...)` yoki `Sheet(...)` chaqiruvi.

## Muammolar

- **Panel bo'sh** — `Addons\ReklamaRasxod` dagi fayllarni *Properties → Unblock* qiling (build.bat buni o'zi qiladi).
- **"Reklama Maket manba kodi topilmadi"** (yig'ishda) — `ReklamaRasxod` va `ReklamaMaket` papkalari yonma-yon bo'lishi kerak.
- **Logo o'lchovi noto'g'ri** — logo orqasida to'rtburchak fon bilan tanlangan bo'lishi mumkin; faqat harflarni tanlang.
  Logo vektor (egri chiziq yoki matn) bo'lishi yaxshi; rasm (bitmap) bo'lsa, shaffof fonli bo'lsin.
- **CSV Excel'da bitta ustunda chiqsa** — Excel'da *Data → From Text/CSV* orqali oching, ajratuvchi — nuqtali vergul (;).
