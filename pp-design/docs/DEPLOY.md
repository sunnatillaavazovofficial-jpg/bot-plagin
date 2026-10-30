# Serverga joylash (VPS + pp-design.duckdns.org + HTTPS)

Quyidagi qadamlar **Ubuntu 22.04 / 24.04** VPS uchun (masalan: Hetzner, DigitalOcean, Timeweb, UzCloud).
Minimal talab: 1 vCPU, 2 GB RAM, 20 GB disk.

---

## 1. DuckDNS domenini serverga ulash

1. https://www.duckdns.org ga kiring (Google/GitHub orqali).
2. `pp-design` subdomeni sizda bo'lsa, **current ip** maydoniga VPS'ning IP manzilini yozib, **update ip** bosing.
3. Sahifaning yuqorisidagi **token**ni saqlab qo'ying (IP o'zgarsa avtomatik yangilash uchun kerak).
4. Tekshirish (kompyuteringizda):
   ```bash
   ping pp-design.duckdns.org
   ```
   VPS IP manzili chiqishi kerak.

> VPS'ning IP manzili doimiy bo'lsa, 3-qadam ixtiyoriy. Agar o'zgaruvchan bo'lsa, serverda har 5 daqiqada yangilab turing:
> ```bash
> (crontab -l 2>/dev/null; echo '*/5 * * * * curl -s "https://www.duckdns.org/update?domains=pp-design&token=SIZNING_TOKEN&ip=" >/dev/null') | crontab -
> ```

## 2. Serverga kerakli dasturlarni o'rnatish

```bash
ssh root@SERVER_IP
```

```bash
apt update && apt upgrade -y
apt install -y git nginx postgresql certbot python3-certbot-nginx
curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && apt install -y nodejs
npm install -g pm2
```

## 3. PostgreSQL bazasini yaratish

`KUCHLI_PAROL` o'rniga o'zingizning parolingizni yozing:

```bash
sudo -u postgres psql -c "CREATE USER ppdesign WITH PASSWORD 'KUCHLI_PAROL';"
```

```bash
sudo -u postgres createdb -O ppdesign ppdesign
```

## 4. Loyihani yuklab olish va sozlash

```bash
cd /opt && git clone https://github.com/sunnatillaavazovofficial-jpg/bot-plagin.git
```

```bash
cd /opt/bot-plagin/pp-design && cp .env.example .env && nano .env
```

`.env` faylda kamida quyidagilarni to'ldiring (to'liq ro'yxat `.env.example` ichida izohlari bilan):

| O'zgaruvchi | Qiymat |
|---|---|
| `SITE_URL` | `https://pp-design.duckdns.org` |
| `DATABASE_URL` | `postgresql://ppdesign:KUCHLI_PAROL@localhost:5432/ppdesign` |
| `SESSION_SECRET` | `openssl rand -hex 32` buyrug'i bergan qator |
| `UPLOAD_DIR` | `/opt/pp-design-uploads` (yangilanishda o'chib ketmasligi uchun loyihadan tashqarida) |
| `TELEGRAM_BOT_TOKEN`, `TELEGRAM_BOT_USERNAME` | 7-bo'limga qarang |
| `TELEGRAM_ADMIN_IDS` | sizning Telegram ID raqamingiz (7-bo'lim) |
| `TELEGRAM_WEBHOOK_SECRET` | `openssl rand -hex 16` |
| `ADMIN_PASSWORD` | zaxira admin paroli |
| `ESKIZ_EMAIL`, `ESKIZ_PASSWORD` | 8-bo'limga qarang |

`DEV_LOGIN` productionda doim `0` bo'lsin.

## 5. O'rnatish, bazani tayyorlash va build

```bash
npm ci
```

```bash
npx prisma migrate deploy
```

Demo ma'lumotlar (6 ta namunaviy dizayner, ishlar, hamkorlar) kerak bo'lsa — ixtiyoriy, keyin admin paneldan o'chirasiz:

```bash
npm run db:seed
```

```bash
npm run build
```

## 6. Saytni ishga tushirish (PM2) va Nginx + HTTPS

```bash
pm2 start ecosystem.config.cjs && pm2 save && pm2 startup
```

```bash
cp docs/nginx.conf /etc/nginx/sites-available/pp-design && ln -s /etc/nginx/sites-available/pp-design /etc/nginx/sites-enabled/ && rm -f /etc/nginx/sites-enabled/default && nginx -t && systemctl reload nginx
```

Let's Encrypt bepul SSL sertifikati (email so'raydi, keyin avtomatik yangilanib turadi):

```bash
certbot --nginx -d pp-design.duckdns.org
```

Endi https://pp-design.duckdns.org ochilishi kerak.

## 7. Telegram bot

1. Telegram'da **@BotFather** → `/newbot` → nom va username bering (masalan `ppdesign_bot`). Bergan **token**ni `.env` dagi `TELEGRAM_BOT_TOKEN` ga, username'ni `TELEGRAM_BOT_USERNAME` ga yozing.
2. @BotFather → `/setdomain` → botingizni tanlang → `pp-design.duckdns.org` yuboring. (Busiz saytdagi «Telegram orqali kirish» tugmasi ishlamaydi.)
3. Webhook'ni ulang:
   ```bash
   npm run webhook:set
   ```
4. Botga `/start` yozing — u sizning **Telegram ID** raqamingizni ko'rsatadi. Shu raqamni `.env` dagi `TELEGRAM_ADMIN_IDS` ga yozing (bir nechta admin bo'lsa vergul bilan).
5. `.env` o'zgargandan keyin qayta ishga tushiring:
   ```bash
   pm2 restart pp-design
   ```

Endi:
- yangi dizayner ro'yxatdan o'tsa — sizga **✅ Tasdiqlash / ❌ Rad etish** tugmali xabar keladi;
- mijoz otziv qoldirsa — otziv dizaynerning o'ziga va adminlarga keladi;
- botda `/pending` — kutilayotgan so'rovlar ro'yxati.

## 8. SMS (Eskiz.uz)

1. https://eskiz.uz da ro'yxatdan o'ting va balansni to'ldiring.
2. Kabinetda **SMS shablon** yuboring, masalan: `PP Design saytiga kirish kodi: 123456`. Eskiz tasdiqlagach, `.env` dagi `SMS_TEMPLATE` ni aynan shu matnga moslang (`{code}` kod o'rniga qo'yiladi).
3. `.env` ga `ESKIZ_EMAIL` va `ESKIZ_PASSWORD` yozing, keyin `pm2 restart pp-design`.

> Eskiz akkaunti "test" holatida faqat ularning test matnini yuboradi — shablon tasdiqlanmaguncha mijozlarga kod bormaydi.

## 9. Admin panelga kirish

https://pp-design.duckdns.org/admin — Telegram orqali (ID'ingiz `TELEGRAM_ADMIN_IDS` da bo'lsa) yoki `ADMIN_PASSWORD` bilan.

Birinchi ishlar:
1. **Matnlar va logo** — logo yuklang, nom va matnlarni yozing.
2. **Aloqa va manzil** — telefon, manzil va xarita koordinatalari.
3. **Ishlar** va **Hamkorlar** — demo ma'lumotlarni o'chirib, haqiqiylarini qo'shing.

## 10. Yangilash (keyingi o'zgarishlardan keyin)

```bash
cd /opt/bot-plagin && git pull && cd pp-design && npm ci && npx prisma migrate deploy && npm run build && pm2 reload pp-design
```

## 11. Zaxira nusxa (backup)

Har kecha soat 3:00 da baza va yuklangan fayllarni saqlash:

```bash
mkdir -p /opt/backups && (crontab -l 2>/dev/null; echo '0 3 * * * pg_dump -U postgres ppdesign | gzip > /opt/backups/db-$(date +\%F).sql.gz && tar czf /opt/backups/uploads-$(date +\%F).tgz -C /opt pp-design-uploads && find /opt/backups -mtime +14 -delete') | crontab -u postgres -
```

> `postgres` foydalanuvchisi `/opt/backups` va `/opt/pp-design-uploads` ga yozish/o'qish huquqiga ega bo'lishi kerak: `chown postgres /opt/backups`.

## Muammolar

| Belgi | Yechim |
|---|---|
| «Telegram orqali kirish» tugmasi chiqmaydi | `TELEGRAM_BOT_USERNAME` to'g'rimi? @BotFather'da `/setdomain` qilinganmi? |
| Bot javob bermaydi | `npm run webhook:set` natijasida xato yo'qmi? `pm2 logs pp-design` |
| Tasdiqlash xabari kelmaydi | `TELEGRAM_ADMIN_IDS` da ID'ingiz bormi va botga `/start` yozganmisiz? |
| SMS kelmaydi | Eskiz balans va shablon tasdiqlanganmi? `pm2 logs pp-design` |
| Video yuklanmaydi (413 xato) | Nginx'da `client_max_body_size 100M;` bormi? |
