# Reklama Maket — CorelDRAW plagini (Channel letters)

Tanlangan logodan **hajmli harf (channel letters)** dizaynini Corelda chizadi: harf dizayni va
fon dizayni alohida tanlanadi, o'lchamlar qo'yiladi. Kerak bo'lsa, tayyor dizaynni **ChatGPT**ga
yuborib, uning kunduzgi va kechki **real ko'rinishini** oladi (har kim o'z API kalitini ulaydi).

## Ish tartibi

1. Corelda logoni belgilang → panelda **Tanlangan logoni olish**.
2. Logoning haqiqiy **bo'yi**ni kiriting — eni proporsiya bo'yicha o'zi hisoblanadi.
3. **Harf dizayni** (harf turi, qalinligi, yuz/bort/nur ranglari) va **fon dizayni**ni tanlang.
   Panel tepasida **Kunduzi / Kechasi** ko'rinishi darhol yangilanadi.
4. **CORELDA DIZAYN CHIZISH** — yangi sahifada quyidagilar chiziladi:
   - kunduzgi ko'rinish (fon + harflar + soya);
   - kechki ko'rinish (nima yonishi: yuz, kontrajur, yon tomon, LED nuqtalar...);
   - yon kesim sxemasi (yuz, bort, orqa, LED, distansiya, qalinlik);
   - o'lcham chiziqlari va sarlavha.
   Hammasi `Ctrl+Z` bilan bitta qadamda bekor qilinadi. Asl logo o'zgarmaydi.
5. **CHATGPT: REAL KO'RINISH** — dizaynni ChatGPT'ga yuboradi. Plagin joy (bino fasadi) rasmini
   tanlashni so'raydi: rasm berilsa, belgi shu fasadga qo'yiladi; berilmasa — umumiy fasadda
   ko'rsatiladi. Natija (kunduzi + kechasi) Corelga qo'shiladi va
   `Hujjatlar\ReklamaMaket\ChatGPT\<sana>` papkasiga saqlanadi.
   **"Dizayn chizilgach avtomatik yuborish"** belgilansa, 4-qadamdan keyin o'zi yuboradi.

## Qoidalar (ogohlantirish beriladi)

| Shart | Natija |
|---|---|
| Logo bo'yi **20 sm dan past** | Hajmli (3D) va yoritilgan qilib bo'lmaydi — faqat yassi harf |
| Qalinligi **4 sm dan kam** | Yoritib bo'lmaydi (ichiga LED sig'maydi) |
| Yonadigan fon (laytboks, gulli list, setka, reyka) qalinligi 4 sm dan kam | Fon yoritilmaydi |

Ogohlantirish panelda sariq qutida ko'rinadi, chizishdan oldin yana so'raladi. Davom etilsa,
dizayn shu cheklov bilan (yassi va/yoki yoritilmagan holda) chiziladi va ChatGPT so'roviga ham
shunday yoziladi.

## Harf dizaynlari

| Nomi | English | Nima yonadi |
|---|---|---|
| Old tomoni yonadigan | Front-lit channel letters | yuz |
| Qiya qirrali, old tomoni yonadi | Face-lit letters with bevel edge | yuz |
| Oq yuz, qora bort | Black & white face-lit letters | yuz |
| Yaxlit akril, yumaloq qirra | Solid acrylic letters, round edge, fully lit | yuz + yon |
| Yaxlit rangli akril, to'g'ri qirra | Solid coloured acrylic letters, square edge, fully lit | yuz + yon |
| Old + orqa yonadigan | Face & halo-lit letters | yuz + kontrajur |
| Orqadan yonadigan (kontrajur) | Halo-lit (reverse channel) letters | kontrajur |
| Yon tomoni qisman yonadi | Partial side-lit letters | yonda chiziq |
| Old + yon qisman yonadi | Face & partial side-lit letters | yuz + yonda chiziq |
| Yon tomoni to'liq yonadi | Side-lit letters (opaque face) | yon |
| Hamma tomoni yonadi | Front & side fully lit letters | yuz + yon |
| Ochiq LED (skelet) | Exposed LED (skeleton) letters | LED nuqtalar |
| Lampochkali (marquee) | Marquee bulb letters | lampochkalar |
| Perforatsiyali + cheti yonadi | Perforated pixel-LED letters with lit rim | piksellar + kontur |
| Naqshli lazer kesma yuz | Laser-cut pattern face letters | naqsh |
| Akril harf, yoritilmaydi | Non-illuminated acrylic letters on standoffs | — |
| Metall harf, yoritilmaydi | Non-illuminated metal letters | — |

## Fon dizaynlari (10)

| Nomi | Izoh |
|---|---|
| Fonsiz (to'g'ridan-to'g'ri devorga) | harflar devorda |
| Kompozit panel (ACP) | tekis panel |
| Kasseta (bortli panel) | chetlari bukilgan panel |
| Laytboks fon (yoritiladigan) | butun yuzi yonadi |
| Akril plita (distansiyada) | shaffof plita, boltlarda |
| Gulli temir list (perforatsiya) | 30 xil gul, orqasiga LED qo'yish mumkin |
| Ramkadagi setka (temir ramka) | profil quvur ramka + setka, oraliq ustunlar |
| Yog'och ramkali setka (orqadan yonadi) | yog'och ramka, setka, chetlari bo'ylab nur |
| Gorizontal temir reykalar (lamel) | kronshteyndagi reykalar, oralig'idan devor ko'rinadi |
| Vertikal reykali panel | qovurg'ali fasad qoplamasi |

**Gullar (30 ta)** — gulli list va setkalar uchun: olti burchakli setka (5 xil qalinlik), dumaloq teshik (5),
xochsimon teshik (4), kvadrat teshik va kvadrat setka (3), uzunchoq tirqish (4), romb setka / prosechka (9).
Panelda har bir gul kichik rasmi bilan tanlanadi.

**Fon yoritilishi (qaysi joyi yonadi)** — gulli, setkali va reykali fonlar uchun: yonmaydi, butunlay yonadi,
chetlari bo'ylab yonadi, logo atrofi yonadi, pastdan yonadi. Nur teshiklar (yoki reykalar oralig'i) orqali
chiqadi; kechki ko'rinishda yorug'lik darajasi bo'yicha chiziladi. Fon qalinligi 4 sm dan kam bo'lsa —
yoritib bo'lmaydi (ogohlantirish beriladi).

## ChatGPT'ni ulash

1. [platform.openai.com/api-keys](https://platform.openai.com/api-keys) sahifasida API kalit yarating
   (panelda **API kalit olish →** havolasi shu sahifani ochadi).
2. OpenAI hisobingizda **Billing** (to'lov) yoqilgan bo'lishi kerak — har bir rasm uchun sizning hisobingizdan
   to'lanadi. Rasm modellari uchun ba'zan tashkilotni tasdiqlash
   ([Organization verification](https://platform.openai.com/settings/organization/general)) ham so'raladi.
3. Kalitni panelga kiritib, **Saqlash** → **Tekshirish**. Kalit faqat shu kompyuterda, Windows foydalanuvchingiz
   uchun shifrlangan holda saqlanadi (`%APPDATA%\ReklamaMaket\settings.json`).

ChatGPT'ga dizayn rasmi bilan birga **plagin dizayni tavsifi** ham yuboriladi: rasm qanday o'qilishi
(plagin chizgan texnik old ko'rinish, haqiqiy rang va proporsiya), harf turi va materiallari, ranglar
(nomi va hex kodi), qalinlik, logo o'lchami va joylashuvi, fon turi, gul, ramka, fon o'lchami,
qaysi joyi yonishi va qat'iy talablar (logoni o'zgartirmaslik, ortiqcha narsa qo'shmaslik).
Panelda **"Yuboriladigan to'liq so'rovni ko'rish"** bo'limida matnning o'zini ko'rish mumkin.

Standart model — `gpt-image-2`; ro'yxatda `gpt-image-2.5-flare`, `gpt-image-2.5-sunburst`,
`chatgpt-image-latest` ham bor, yangi model nomini qo'lda yozish mumkin. **So'rov (prompt)** maydonini
o'zgartirsangiz saqlanib qoladi; **Standart** tugmasi asl matnni qaytaradi. Harf/fon turi, o'lcham,
qoidalar va kunduzi/kechasi so'rovga avtomatik qo'shiladi.

## O'rnatish

Talablar: Windows 10/11, CorelDRAW X7 yoki yangisi (64-bit), hamda bittasi:
[.NET SDK](https://dotnet.microsoft.com/download) **yoki** Visual Studio 2022 (".NET desktop development").

1. CorelDRAW'ni yoping.
2. `ReklamaMaket\build.bat` ni ikki marta bosing — skript Corel'ni topadi, plaginni yig'adi va
   `...\Programs64\Addons\ReklamaMaket\` papkasiga nusxalaydi (administrator ruxsatini so'raydi).
3. CorelDRAW'ni oching: **Reklama → Reklama Maket** menyusi yoki standart asboblar panelidagi tugma.
   Ko'rinmasa — CorelDRAW'ni **F8** bosib turgan holda ishga tushiring.

Qo'shimcha: `build.bat -CorelPath "C:\...\Programs64"`, `build.bat -NoInstall`, `build.bat -Uninstall`.

## Loyiha tuzilishi

```
ReklamaMaket/
├── build.bat, build.ps1            yig'ish va o'rnatish
└── src/ReklamaMaket/
    ├── AppUI.xslt, UserUI.xslt     Corel paneli, menyu va tugma
    ├── DockerUI.xaml(.cs)          panel
    ├── Styles/Theme.xaml           panel ranglari va uslublari
    ├── Designs/LetterStyle.cs      harf dizaynlari ro'yxati
    ├── Designs/BackgroundStyle.cs  fon dizaynlari ro'yxati
    ├── Designs/PatternStyle.cs     gullar (perforatsiya, setka) ro'yxati
    ├── Core/                       sozlamalar, qoidalar (SignRules), logo olish/eksport
    ├── Rendering/                  Corelda chizish va paneldagi ko'rinish
    └── Ai/                         ChatGPT (OpenAI Images API) va so'rov matni
```

## Yangi harf turi qo'shish

`Designs/LetterStyle.cs` dagi `LetterStyles.All` ro'yxatiga bitta element qo'shing — panel,
Corel chizmasi, kechki ko'rinish, kesim va ChatGPT so'rovi avtomatik ishlaydi:

```csharp
new LetterStyle("Neon chiziqli", "Neon-trim letters",
    "Harf cheti bo'ylab neon lenta.",
    LightZones.Rim,                 // nima yonadi (Face, Halo, SideFull, SidePartial, Rim, Dots, Pattern)
    FaceMaterial.Metal,             // yuz materiali
    Rgb(0x222222), Rgb(0x222222), Rgb(0xFF2DCE), // yuz, bort, nur ranglari
    6, 50,                          // ko'rinadigan chet (mm), standart qalinlik (mm)
    "Yuz: metall", "Bort: metall + neon",
    "letters with a glowing neon strip along the outline"), // ChatGPT uchun tavsif
```

## Muammolar

- **Panel bo'sh** — `Addons\ReklamaMaket` dagi fayllarni *Properties → Unblock* qiling (build.bat buni o'zi qiladi).
- **"Logoni yaxlit konturga aylantirib bo'lmadi"** — logoda rasm (bitmap) bor; avval *Trace Bitmap* qiling.
- **ChatGPT 401** — kalit noto'g'ri; **403** — tashkilotni tasdiqlash kerak; **429** — limit yoki balans tugagan.
