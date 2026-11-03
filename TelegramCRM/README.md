# TelegramCRM

Bir nechta Telegram akkauntni **bitta saytda** birlashtiradigan va ishlarni tizimlashtiradigan dastur.

- **Yagona chat oynasi** — barcha akkauntlarga kelgan xabarlar bitta ro'yxatda. Har bir chatda u qaysi akkauntga kelgani rangli belgi bilan ko'rsatiladi. Javobni shu yerdan matn, rasm yoki fayl qilib yozasiz. Filtrlar: o'qilmagan, javobsiz, menga biriktirilgan, akkaunt bo'yicha, qidiruv.
- **Zakazlar doskasi** — xabardan bir tugma bilan zakaz ochiladi, xabardagi rasm va fayllar zakazga o'tadi. Zakazlar holat ustunlarida turadi (Yangi → Dizayn → … → Topshirildi) va sichqoncha bilan suriladi. Har bir zakazda narx, to'lovlar, qarz, muddat, mas'ul xodim, fayllar, izohlar va o'zgarishlar tarixi saqlanadi. Maketni zakaz ichidan mijozga Telegram orqali yuborish mumkin.
- **Mijozlar bazasi** — mijoz qaysi akkauntlarga yozgan bo'lsa ham, uning hamma chatlari, zakazlari, jami summasi va qarzi bitta kartada. Bir odam uchun ikki karta ochilib qolsa, ularni birlashtirish mumkin.
- **Hisobot** — tanlangan davr bo'yicha tushum, zakazlar, o'rtacha chek, qarzdorlik, muddati o'tganlar. Kunlik grafik, to'lov turlari, eng yaxshi mijozlar, akkauntlar va xodimlar kesimi.
- **Eslatmalar (Telegram bot)** — javobsiz qolgan chatlar, ertaga va bugun muddati tugaydigan hamda muddati o'tgan zakazlar, xodimga yangi zakaz biriktirilgani haqida xabar beradi. Adminlarga har kuni qisqa hisobot yuboradi.
- **Xodimlar va ruxsatlar** — har bir xodimning o'z logini bor. Har kim faqat o'ziga ruxsat berilgan akkauntlarni ko'radi.
- Kompyuterda ham, telefonda ham ishlaydi. Yangi xabarlar sahifani yangilamasdan, darhol ko'rinadi.

![Chatlar](docs/chatlar.png)

![Zakazlar doskasi](docs/zakazlar.png)

![Hisobot](docs/hisobot.png)

| Rol | Nima qila oladi |
|---|---|
| Admin | Hammasi: akkauntlarni ulash, xodimlar, holatlar, hisobot, sozlamalar |
| Menejer | Ruxsat berilgan akkauntlar chatlari, zakazlar, mijozlar, to'lovlar |
| Xodim | Faqat o'ziga biriktirilgan zakazlar: holatni o'zgartirish, izoh, fayl. Ruxsat berilsa, akkaunt chatlari ham |

## Qanday ishlaydi

Oddiy Telegram akkauntlarni bot orqali o'qib bo'lmaydi. Shuning uchun sayt Telegram'ning rasmiy
client API'sidan (MTProto, [Telethon](https://docs.telethon.dev) kutubxonasi) foydalanadi. Bu Telegram
Desktop kabi yana bitta "qurilma" sifatida ulanish degani. Har bir akkaunt saytga bir marta
telefon raqam, kod va kerak bo'lsa ikki bosqichli parol bilan ulanadi.

- Sessiyalar serverda **shifrlangan** holda saqlanadi (kalit — `.env` dagi `SECRET_KEY`).
- Telegram'ning kirish kodlari keladigan xizmat chati (777000) va «Saqlangan xabarlar» saytda **ko'rsatilmaydi**, shuning uchun xodimlar kodlarni ko'ra olmaydi.
- Akkaunt ulanganda Telegram «yangi qurilmadan kirildi» deb xabar yuboradi. Bu odatiy hol. Ulanishni istalgan vaqt telefondan uzish mumkin: Sozlamalar → Qurilmalar → TelegramCRM.
- Saytda hammaga birdan xabar tarqatish (rassilka) funksiyasi **yo'q** va qo'shilmaydi, chunki bunday tarqatish uchun Telegram akkauntlarni bloklaydi.
- Odatiy holatda faqat shaxsiy chatlar ko'rinadi. Guruhlarni akkaunt sozlamasida yoqish mumkin. Kanallar va botlar ko'rsatilmaydi.

## Tayyorgarlik (bir marta)

1. **Telegram API kaliti.** [my.telegram.org](https://my.telegram.org) saytiga istalgan akkauntingiz bilan kiring → «API development tools» → ilova yarating (nomi ixtiyoriy). `api_id` va `api_hash` ni yozib oling. Bitta kalit barcha akkauntlar uchun yetadi.
2. **Eslatma boti (ixtiyoriy).** Telegram'da [@BotFather](https://t.me/BotFather) → `/newbot` → bot tokenini oling.
3. **Server (VPS).** Ubuntu 22.04 yoki 24.04, kamida 1 GB RAM (taxminan oyiga 5$, masalan Hetzner, DigitalOcean yoki mahalliy hosting).
4. **Domen (tavsiya etiladi).** Masalan `crm.sizning-domeningiz.uz`. Domenning A-yozuvini server IP manziliga yo'naltiring. Domen bo'lsa sayt avtomatik HTTPS bilan ishlaydi.

## Serverga o'rnatish

Serverga SSH orqali kiring va Docker'ni o'rnating:

```bash
curl -fsSL https://get.docker.com | sh
```

Kodni yuklab oling:

```bash
git clone https://github.com/sunnatillaavazovofficial-jpg/bot-plagin.git
```

```bash
cd bot-plagin/TelegramCRM
```

Sozlamalar faylini yarating:

```bash
cp .env.example .env
```

Maxfiy kalit yarating va chiqqan satrni `.env` dagi `SECRET_KEY=` ga qo'ying:

```bash
openssl rand -hex 32
```

`.env` faylini to'ldiring: `ADMIN_PASSWORD`, `TG_API_ID`, `TG_API_HASH`, `BOT_TOKEN`, `DOMAIN`.

```bash
nano .env
```

Ishga tushiring:

```bash
docker compose up -d --build
```

Bir-ikki daqiqadan keyin brauzerda `https://sizning-domeningiz` manzilini oching. Domen bo'lmasa `http://SERVER_IP` manzilini oching, bu holda `.env` da `COOKIE_SECURE=0` bo'lsin. `admin` login va `.env` dagi parol bilan kiring.

### Birinchi sozlash

1. **Sozlamalar → Telegram akkauntlar → Akkaunt qo'shish.** Telefon raqamni kiriting, keyin shu akkauntning Telegram ilovasiga kelgan kodni yozing. Har bir akkauntga nom va rang bering.
2. **Sozlamalar → Xodimlar.** Har bir xodimga login yarating va qaysi akkauntlarni ko'rishini belgilang.
3. **Sozlamalar → Zakaz holatlari.** Ustunlarni o'z ish jarayoningizga moslang.
4. **Sozlamalar → Profil → Botni ulash.** Har bir xodim o'zi bosadi, shunda eslatmalar unga keladi.
5. **Sozlamalar → Eslatmalar.** Kunlik hisobot vaqti va javobsiz chat uchun daqiqa chegarasini sozlang.

## Kundalik ishlar

Yangilash (yangi versiya chiqqanda):

```bash
git pull && docker compose up -d --build
```

Loglarni ko'rish:

```bash
docker compose logs -f app
```

Zaxira nusxalar: baza har kuni soat 03:00 da avtomatik nusxalanadi (oxirgi 14 kun saqlanadi).
Ularni konteynerdan server papkasiga ko'chirish (keyin `scp` bilan kompyuterga yuklab olasiz):

```bash
docker compose cp app:/data/backups ./backups
```

Barcha ma'lumotlar (baza, fayllar, sessiyalar) `crm-data` nomli Docker hajmida saqlanadi.
`docker compose down` ularni o'chirmaydi. `docker compose down -v` esa **hammasini o'chiradi**, ehtiyot bo'ling.

> `SECRET_KEY` ni o'zgartirmang. O'zgarsa, saqlangan sessiyalarni o'qib bo'lmaydi va barcha akkauntlarni qayta ulash kerak bo'ladi.

## Sinov rejimi (Telegram'siz)

`.env` da `TG_FAKE=1` qilsangiz, sayt Telegram'ga ulanmaydi va demo akkauntlar, chatlar bilan ishlaydi.
Har 45 soniyada «mijoz»dan yangi xabar keladi. Yangi akkaunt qo'shishda kod `12345` bo'ladi.
Bu rejim saytni ko'rib chiqish va xodimlarni o'rgatish uchun.

## Dasturchilar uchun

```
TelegramCRM/
  backend/      FastAPI + Telethon + SQLAlchemy (SQLite), Alembic migratsiyalari, testlar
    app/telegram/manager.py   haqiqiy akkauntlar (MTProto): kirish, sinxron, hodisalar, yuborish, media
    app/telegram/fake.py      sinov rejimi
    app/telegram/store.py     xabar va chatlarni bazaga yozish (ikkala rejim uchun umumiy)
    app/routers/              REST API va /ws (WebSocket)
    app/notify/               eslatma boti va fon vazifalari (eslatmalar, zaxira nusxa)
  frontend/     React + Vite + TypeScript + Tailwind
  Dockerfile, docker-compose.yml, Caddyfile
```

Backend'ni sinov rejimida ishga tushirish:

```bash
cd backend && pip install -r requirements-dev.txt
```

```bash
TG_FAKE=1 DATA_DIR=./data uvicorn app.main:app --reload --port 8000
```

Frontend (alohida terminalda; `/api` va `/ws` so'rovlari 8000-portga yo'naltiriladi):

```bash
cd frontend && npm install && npm run dev
```

Testlar:

```bash
cd backend && python -m pytest -q
```

Baza tuzilmasini o'zgartirgandan keyin yangi migratsiya yarating
(ilova har ishga tushganda migratsiyalarni o'zi qo'llaydi):

```bash
cd backend && TG_FAKE=1 DATA_DIR=./data python -c "from alembic import command; from app.migrate import upgrade_database, alembic_config as c; upgrade_database(); command.revision(c(), message='ozgarish', autogenerate=True)"
```

Muhim: ilova **bitta jarayonda** ishlashi kerak (`uvicorn` ga `--workers` bermang), chunki
Telegram ulanishlari va real vaqt xabarlari shu jarayon xotirasida turadi.
