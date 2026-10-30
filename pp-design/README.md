# PP Design — dizaynerlar platformasi

**pp-design.duckdns.org** uchun veb-sayt: dizaynerlar o'z profillarini yuritadi, mijozlar ularga otziv qoldiradi,
otzivlar va yangi dizaynerlar so'rovlari Telegram botga keladi. Bosh sahifa dizayn tarixi haqidagi ovozli intro bilan ochiladi.

## Imkoniyatlar

| Bo'lim | Nima qiladi |
|---|---|
| **Intro (~10 s)** | G'or rasmlari → Misr ierogliflari → Uyg'onish davri → kitob bosish va plakatlar → kompyuter grafikasi → AI dizayn. Canvas'da chiziladi, ovozlar Web Audio bilan sintez qilinadi. «Boshlash (ovoz bilan)», «Ovozsiz ko'rish», «O'tkazib yuborish». Admin paneldan tayyor MP4 yuklasangiz, o'sha video o'ynaydi. |
| **AI robot** | Bosh ekranda: boshi va ko'zlari sichqoncha (telefonda — barmoq) tomonga qaraydi, ko'z qisadi, yonida asosiy gaplar yozilib turadi. |
| **Dizaynerlar lentasi** | Pastga tushganda kartochkalar chegarasi «chiziladi», lenta chapdan o'ngga uzluksiz aylanadi (ustiga borilsa to'xtaydi), bosilsa profilga o'tadi. |
| **Kompaniya, ishlar, hamkorlar, aloqa** | Kompaniya haqida, raqamlar va xizmatlar; kategoriyali portfolio galereyasi; hamkorlar logolari lentasi; xarita (Yandex/Google), telefon, Telegram, Instagram. |
| **Mijoz** | Faqat telefon raqami + SMS kod (Eskiz.uz) bilan kiradi, dizaynerga 1–5 yulduz va matnli otziv qoldiradi. |
| **Dizayner** | Telegram orqali ro'yxatdan o'tadi → so'rov adminga botda tugmalar bilan boradi → tasdiqlangach profili saytda chiqadi. Profilini o'zi to'ldiradi: rasm, mutaxassislik, o'zi haqida, tajriba, ko'nikmalar, portfolio, narxlar, ijtimoiy tarmoqlar. |
| **Admin** | Telegram (ID ro'yxatda bo'lsa) yoki parol bilan kiradi. Dizaynerlarni tasdiqlash/rad etish/bloklash, otzivlarni o'chirish, matnlar, logo, ishlar, hamkorlar, aloqa, intro video. |
| **Telegram bot** | Yangi dizayner so'rovi (tasdiqlash tugmalari bilan), har bir otziv (dizaynerga va adminga), dizaynerga tasdiqlangani haqida xabar, `/start` (Telegram ID'ni ko'rsatadi), `/pending`. |
| **SEO** | Sahifa metamalumotlari, Open Graph rasm, `sitemap.xml`, `robots.txt`, schema.org (Organization, Person + reyting). |

## Texnologiyalar

- **Next.js 16** (App Router, Server Actions) + **React 19** + **TypeScript**
- **Tailwind CSS 4**
- **PostgreSQL** + **Prisma 6**
- **grammY** — Telegram bot (production'da webhook, lokalda polling)
- **sharp** — yuklangan rasmlarni siqish va WebP ga o'girish
- **Eskiz.uz** — SMS

Frontend va backend bitta Next.js ilovasida: alohida server kerak emas, bot ham shu ilova ichida webhook orqali ishlaydi.

## Lokal ishga tushirish

Kerak: Node.js 22+, PostgreSQL 14+.

```bash
cd pp-design && npm install
```

```bash
cp .env.example .env
```

`.env` da `DATABASE_URL` ni yozing va sinov uchun `DEV_LOGIN=1` qiling (SMS kod ekranda chiqadi, login sahifasida «Sinov rejimi» tugmalari paydo bo'ladi — Telegram widget localhost'da ishlamaydi).

```bash
npx prisma migrate dev
```

```bash
npm run db:seed
```

```bash
npm run dev
```

http://localhost:3000 — sayt, http://localhost:3000/admin — admin panel.

Botni lokalda sinash (`TELEGRAM_BOT_TOKEN` kerak):

```bash
npm run bot
```

## Serverga joylash

Qadamma-qadam: **[docs/DEPLOY.md](docs/DEPLOY.md)** (VPS, DuckDNS, PostgreSQL, PM2, Nginx, Let's Encrypt HTTPS, Telegram webhook, Eskiz).

## Logo ranglarini saytga qo'llash

Hozir vaqtinchalik «PP» belgisi va ranglar turibdi. Haqiqiy logo tayyor bo'lganda:

1. **Admin panel → Matnlar va logo** dan logoni yuklang (sayt menyusi va footerda chiqadi).
2. Ranglarni `src/app/globals.css` faylining boshidagi uchta qatorda almashtiring:
   ```css
   --color-brand: #38c8ff;   /* logoning asosiy rangi */
   --color-brand-2: #7b61ff; /* ikkinchi rang */
   --color-brand-3: #ff5ccd; /* aksent rang */
   ```
   Tugmalar, gradientlar, robot, kartochkalar va intro animatsiya shu ranglardan avtomatik foydalanadi.
3. `public/logo.svg` (favicon) ni ham logoning SVG varianti bilan almashtiring.

## Intro video

Kod animatsiyasi o'rniga AI bilan yaratilgan video qo'ymoqchi bo'lsangiz — Sora/Veo/Runway uchun tayyor
sahnama-sahna prompt va musiqa prompti: **[docs/intro-video-prompt.md](docs/intro-video-prompt.md)**.

## Loyiha tuzilishi

```
pp-design/
├── prisma/
│   ├── schema.prisma        # baza: User, Designer, PortfolioItem, Review, OtpCode, Project, Partner, Setting
│   ├── migrations/
│   └── seed.ts              # demo ma'lumotlar
├── scripts/
│   ├── bot.ts               # botni polling rejimida ishga tushirish
│   └── set-webhook.ts       # productionda webhook'ni ulash
├── src/
│   ├── app/
│   │   ├── (site)/          # ommaviy sahifalar: bosh sahifa, /designer/[username], /designers, /login, /dashboard
│   │   ├── admin/           # admin panel va /admin/login
│   │   ├── actions/         # server actions: otziv, dizayner kabineti, admin
│   │   ├── api/             # auth (SMS, Telegram, parol), fayl yuklash, Telegram webhook
│   │   ├── uploads/         # yuklangan fayllarni berish
│   │   ├── sitemap.ts, robots.ts, opengraph-image.tsx
│   │   └── globals.css      # ranglar va umumiy uslublar
│   ├── components/
│   │   ├── intro/           # intro: sahnalar (Canvas) va ovozlar (Web Audio)
│   │   ├── home/            # robot, hero, dizaynerlar lentasi, kompaniya, ishlar, aloqa
│   │   ├── auth/ dashboard/ designer/ admin/ ui/
│   └── lib/                 # auth, sessiya, bot, bildirishnomalar, SMS, yuklash, sozlamalar
├── docs/                    # DEPLOY.md, nginx.conf, intro-video-prompt.md
└── ecosystem.config.cjs     # PM2
```

## Xavfsizlik bo'yicha eslatmalar

- Sessiya — imzolangan JWT, `httpOnly` + `SameSite=Lax` cookie; API so'rovlarida Origin tekshiriladi.
- Telegram login ma'lumotlari bot tokeni bilan HMAC orqali tekshiriladi; admin huquqi faqat `TELEGRAM_ADMIN_IDS` dagi ID'larga.
- SMS kodlar bazada xesh ko'rinishida, 5 daqiqa amal qiladi, 5 ta urinish, qayta yuborish 60 soniyadan keyin, kuniga 8 ta.
- Yuklangan rasmlar qayta kodlanadi (WebP), SVG faqat admin uchun va xavfli teglar tekshiriladi; fayllar `sandbox` CSP bilan beriladi.
- `DEV_LOGIN` productionda hech qachon ishlamaydi (`NODE_ENV=production` da avtomatik o'chiq).
