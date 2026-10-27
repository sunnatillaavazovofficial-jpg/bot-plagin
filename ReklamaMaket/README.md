# Reklama Maket — CorelDRAW plagini

Tanlangan logoni tashqi reklama konstruksiyasiga (laytboks, banner, bilbord, pilon...) joylab,
orqasiga fon chizadi va uni **joyida turgandek** ko'rsatadi: devor yoki ko'cha, yer, soya,
masshtab uchun odam siluyeti va o'lcham chiziqlari bilan. Panelda 2D (old tomoni) va
**3D** ko'rinish bor — konstruksiyani chapdan va o'ngdan aylantirib ko'rish mumkin.

## Paneldagi bo'limlar

| Bo'lim | Nima qiladi |
|---|---|
| **Ko'rinish** (yuqorida, doim ko'rinadi) | 3D sahna: *◀ Chapdan / Old / O'ngdan ▶* tugmalari, burilish va qiyalik slayderlari, sichqoncha bilan aylantirish, g'ildirak bilan yaqinlashtirish, **Kechasi** rejimi (yoritilgan yuz). *Old tomoni (2D)* — logo fon ustida qanday turishi. |
| **1. Logo** | Corelda belgilangan obyekt(lar)ni logo sifatida oladi, kichik rasm va o'lchamini ko'rsatadi. Asl logo o'zgarmaydi. |
| **2. Dizayn turi** | Laytboks, Banner, Bilbord, Pilon + har biri uchun tayyor o'lchamlar. |
| **3. O'lchamlar** | Eni, bo'yi, qalinligi, yerdan balandligi; birlik (mm / sm / m); Corelda chizish masshtabi (1:1 … 1:100). |
| **4. Logo joylashuvi** | Markazda / chapda / o'ngda / tepada / pastda, chetdan bo'sh joy (%), logo hajmi (%). |
| **5. Ranglar** | Fon (logo orqasi) va karkas/profil rangi: tayyor namunalar, `#RRGGBB` yoki rang tanlash oynasi. |
| **6. Qo'shimcha** | Atrof-muhit, odam siluyeti, o'lcham chiziqlari, yangi sahifada yaratish. |

**CORELDA MAKET YARATISH** — hammasini Corel hujjatiga vektor obyektlar qilib chizadi
(bitta guruh, `Ctrl+Z` bilan bitta qadamda bekor qilinadi).
**3D ko'rinishlarni Corelga qo'shish** — chapdan, old va o'ngdan olingan 3 ta rasmni sahifaga qo'yadi.

## O'rnatish

Talablar: Windows 10/11, CorelDRAW X7 yoki yangisi (64-bit), hamda bittasi:
[.NET SDK](https://dotnet.microsoft.com/download) **yoki** Visual Studio 2022 (".NET desktop development").

1. CorelDRAW'ni yoping.
2. `ReklamaMaket\build.bat` faylini ikki marta bosing. Skript:
   - CorelDRAW o'rnatilgan papkani topadi (bir nechta bo'lsa, so'raydi);
   - plaginni yig'adi;
   - `...\Programs64\Addons\ReklamaMaket\` papkasiga nusxalaydi (administrator ruxsatini so'raydi).
3. CorelDRAW'ni oching: asosiy menyuda **Reklama → Reklama Maket** yoki standart asboblar
   panelining oxiridagi **Reklama Maket** tugmasi.

Menyu/tugma ko'rinmasa — CorelDRAW'ni **F8** tugmasini bosib turgan holda ishga tushiring
(workspace yangilanadi) yoki *Tools → Options → Customization → Commands* dan "Reklama Maket"
buyrug'ini istalgan panelga sudrab qo'ying.

Qo'shimcha: `build.bat -CorelPath "C:\...\Programs64"` — papkani qo'lda berish,
`build.bat -NoInstall` — faqat yig'ish, `build.bat -Uninstall` — o'chirish.

## Ishlatish

1. Corelda logoni belgilang → panelda **Tanlangan logoni olish**.
2. **Dizayn turi** va **tayyor o'lcham**ni tanlang (yoki o'lchamni qo'lda kiriting).
3. Logo joylashuvi va fon rangini sozlang — yuqoridagi 3D/2D ko'rinish darhol yangilanadi.
4. **CORELDA MAKET YARATISH** — yangi sahifada (yoki logoning o'ng tomonida) maket paydo bo'ladi.

## Loyiha tuzilishi

```
ReklamaMaket/
├── build.bat, build.ps1          yig'ish va o'rnatish
├── ReklamaMaket.sln
└── src/ReklamaMaket/
    ├── AppUI.xslt, UserUI.xslt   Corel paneli, menyu va tugma e'loni
    ├── CorelDrw.addon            "bu CorelDRAW plagini" belgisi (bo'sh fayl)
    ├── DockerUI.xaml(.cs)        panel (oyna) va uning mantig'i
    ├── Styles/Theme.xaml         panel ranglari va uslublari
    ├── Designs/                  dizayn turlari — har biri alohida fayl
    ├── Model/                    maket modeli (qismlar, atrof-muhit)
    ├── Rendering/                2D yuz rasmi, 3D sahna, Corelda chizish
    └── Core/                     sozlamalar, logo olish, yordamchilar
```

## Yangi dizayn turi qo'shish

Har bir tur — `Designs/` papkasidagi bitta klass. Konstruksiya qutilardan (`Part`) yig'iladi;
o'lchamlar millimetrda, yer sathi `Y = 0`, devor yuzasi `Z = 0`, konstruksiya `X = 0` atrofida.
Shu ma'lumotdan ham Corel chizmasi, ham 3D model avtomatik quriladi.

```csharp
public sealed class VyveskaDesign : DesignType
{
    public override string Name => "Vyveska";
    public override string Description => "Kirish ustidagi panel.";
    public override SceneKind Scene => SceneKind.Facade;     // yoki SceneKind.Street
    public override Color DefaultFrameColor => Color.FromRgb(0x40, 0x40, 0x40);
    public override SizePreset[] Presets => new[]
    {
        //             nomi              eni   bo'yi qalinligi balandligi
        new SizePreset("4000 × 700 mm", 4000, 700, 60, 2600),
    };

    public override void Build(SignModel model)
    {
        var s = model.Settings;
        // Logo turadigan yuz — albatta model.Face ga yoziladi
        model.Face = model.Add(new Part("Yuz", PartKind.Face,
            -s.Width / 2, s.Elevation, 0, s.Width, s.Height, s.Depth, s.FrameColor) { CastsShadow = true });
    }
}
```

So'ng `Designs/DesignCatalog.cs` ro'yxatiga `new VyveskaDesign(),` qatorini qo'shing va `build.bat` ni qayta ishga tushiring.

## Muammolar

- **Panel bo'sh ochiladi** — `Addons\ReklamaMaket` dagi fayllar internetdan kelgan deb bloklangan bo'lishi mumkin:
  har bir faylning *Properties → Unblock* belgisini qo'ying (build.bat buni o'zi qiladi) va Corelni qayta oching.
- **Menyu ko'rinmaydi** — CorelDRAW'ni F8 bilan ishga tushiring.
- **Boshqa CorelDRAW versiyasiga o'tdingiz** — `build.bat` ni qayta ishga tushirib, yangi versiyani tanlang.
- **"Logo topilmadi"** — logo o'chirilgan; uni qayta belgilab, *Tanlangan logoni olish* ni bosing.
