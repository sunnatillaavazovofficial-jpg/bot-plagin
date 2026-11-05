#!/usr/bin/env bash
# TelegramCRM — serverga o'rnatish yoki yangilash (Ubuntu). Qayta ishga tushirish xavfsiz:
# mavjud SECRET_KEY, admin paroli va boshqa sozlamalar saqlanadi, faqat berilgan qiymatlar yangilanadi.
#
# Foydalanish (root sifatida):
#   DOMAIN=mediamax.duckdns.org BOT_TOKEN=123:ABC ADMIN_TG_ID=123456 bash install.sh
#
# Ixtiyoriy o'zgaruvchilar:
#   DOMAIN        sayt domeni (HTTPS avtomatik). Bo'sh bo'lsa sayt http://SERVER_IP da ochiladi
#   WWW_DOMAIN    www.domen → domen yo'naltirish (masalan www.media-max.net)
#   BOT_TOKEN     @BotFather tokeni
#   ADMIN_TG_ID   adminning Telegram ID raqami (bot eslatmalari va kassaga yozish uchun)
#   TG_API_ID, TG_API_HASH   my.telegram.org kalitlari (Telegram akkauntlarni ulash uchun)
#   SITE_PRESET   bosh sahifa uchun tayyor matn (masalan media-max)
#   ENV_FILE      tayyor .env fayli (masalan kompyuterdan yuklangan /root/crm.env). Berilsa, shu fayl ishlatiladi
#                 va admin paroli undagi ADMIN_PASSWORD ga o'rnatiladi
#   RESET_ADMIN=1 admin parolini .env dagi ADMIN_PASSWORD ga qayta o'rnatish
#   INSTALL_DIR   dastur papkasi (standart: ~/bot-plagin)
set -euo pipefail

REPO="https://github.com/sunnatillaavazovofficial-jpg/bot-plagin.git"
BRANCH="claude/blissful-brown-asw2bf"
DIR="${INSTALL_DIR:-$HOME/bot-plagin}"

say() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }

# --- FUNKSIYALAR ---
NGINX_RELOAD="${NGINX_RELOAD:-systemctl reload nginx}"
APACHE_RELOAD="${APACHE_RELOAD:-systemctl reload apache2}"
NGINX_DIR="${NGINX_DIR:-/etc/nginx}"
APACHE_DIR="${APACHE_DIR:-/etc/apache2}"

# Portni egallagan dastur nomi (bo'sh bo'lsa — port bo'sh)
port_owner() {
  ss -ltnpH "sport = :$1" 2>/dev/null | grep -oE 'users:\(\("[^"]+"' | head -n1 | cut -d'"' -f2 || true
}

nginx_conf() {  # $1 domen, $2 ichki port
  cat <<CONF
# TelegramCRM — install.sh yaratgan. Faqat $1 manzili uchun; boshqa saytlarga tegmaydi.
server {
    listen 80;
    server_name $1;
    client_max_body_size 2g;

    location /ws {
        proxy_pass http://127.0.0.1:$2;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_read_timeout 3600s;
    }

    location / {
        proxy_pass http://127.0.0.1:$2;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_read_timeout 300s;
    }
}
CONF
}

apache_conf() {  # $1 domen, $2 ichki port
  cat <<CONF
# TelegramCRM — install.sh yaratgan. Faqat $1 manzili uchun; boshqa saytlarga tegmaydi.
<VirtualHost *:80>
    ServerName $1
    ProxyRequests Off
    ProxyPreserveHost On
    LimitRequestBody 0
    RequestHeader set X-Forwarded-Proto expr=%{REQUEST_SCHEME}
    ProxyPass /ws ws://127.0.0.1:$2/ws
    ProxyPassReverse /ws ws://127.0.0.1:$2/ws
    ProxyPass / http://127.0.0.1:$2/
    ProxyPassReverse / http://127.0.0.1:$2/
</VirtualHost>
CONF
}

# Mavjud nginx'ga shu domen uchun blok qo'shadi. 0 — muvaffaqiyat.
setup_nginx() {  # $1 domen, $2 ichki port
  local file link=""
  if [ -d "$NGINX_DIR/sites-available" ] && [ -d "$NGINX_DIR/sites-enabled" ]; then
    file="$NGINX_DIR/sites-available/telegramcrm.conf"
    link="$NGINX_DIR/sites-enabled/telegramcrm.conf"
  elif [ -d "$NGINX_DIR/conf.d" ]; then
    file="$NGINX_DIR/conf.d/telegramcrm.conf"
  else
    echo "nginx sozlamalar papkasi topilmadi ($NGINX_DIR)."
    return 1
  fi
  if ! nginx -t >/dev/null 2>&1; then
    echo "Serverdagi nginx sozlamalarida avvaldan xato bor (nginx -t) — hech narsa o'zgartirilmadi."
    return 1
  fi
  nginx_conf "$1" "$2" > "$file"
  [ -z "$link" ] || ln -sf "$file" "$link"
  if ! nginx -t >/dev/null 2>&1 || ! nginx -T 2>/dev/null | grep -q "server_name $1;"; then
    echo "nginx yangi blokni qabul qilmadi — u olib tashlandi, mavjud saytlar o'zgarmadi."
    rm -f "$file" ${link:+"$link"}
    return 1
  fi
  $NGINX_RELOAD >/dev/null 2>&1 || nginx -s reload
}

# Mavjud apache'ga shu domen uchun sayt qo'shadi. 0 — muvaffaqiyat.
setup_apache() {  # $1 domen, $2 ichki port
  local file="$APACHE_DIR/sites-available/telegramcrm.conf"
  [ -d "$APACHE_DIR/sites-available" ] || { echo "apache sozlamalar papkasi topilmadi ($APACHE_DIR)."; return 1; }
  if ! apachectl configtest >/dev/null 2>&1; then
    echo "Serverdagi apache sozlamalarida avvaldan xato bor (apachectl configtest) — hech narsa o'zgartirilmadi."
    return 1
  fi
  a2enmod -q proxy proxy_http proxy_wstunnel headers >/dev/null
  apache_conf "$1" "$2" > "$file"
  a2ensite -q telegramcrm >/dev/null
  if ! apachectl configtest >/dev/null 2>&1; then
    echo "apache yangi saytni qabul qilmadi — u olib tashlandi, mavjud saytlar o'zgarmadi."
    a2dissite -q telegramcrm >/dev/null 2>&1 || true
    rm -f "$file"
    return 1
  fi
  $APACHE_RELOAD >/dev/null 2>&1 || apachectl graceful
}

apt_install() {
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq -o DPkg::Lock::Timeout=180 "$@" >/dev/null 2>&1 || {
    apt-get update -qq >/dev/null 2>&1 &&
      DEBIAN_FRONTEND=noninteractive apt-get install -y -qq -o DPkg::Lock::Timeout=180 "$@" >/dev/null 2>&1
  }
}

# Bepul HTTPS sertifikati (Let's Encrypt). Olinmasa sayt http orqali ishlab turadi.
setup_https() {  # $1 nginx|apache, $2 domen
  local plugin="python3-certbot-$1" ip
  say "HTTPS sertifikati olinmoqda ($2)"
  HTTPS_OK=0
  # certbot yo'q bo'lsa yoki apt'dan o'rnatilgan-u, plagini yo'q bo'lsa (snap'dagi certbot'da plaginlar ichida)
  if ! command -v certbot >/dev/null 2>&1 || { dpkg -s certbot >/dev/null 2>&1 && ! dpkg -s "$plugin" >/dev/null 2>&1; }; then
    if ! apt_install certbot "$plugin"; then
      echo "certbot o'rnatilmadi — sayt hozircha http:// orqali ochiladi. Skriptni keyinroq qayta ishga tushiring."
      return 0
    fi
  fi
  if certbot "--$1" -d "$2" --non-interactive --agree-tos --register-unsafely-without-email \
       --redirect --keep-until-expiring >/dev/null 2>&1; then
    HTTPS_OK=1
  else
    ip="$(hostname -I 2>/dev/null | awk '{print $1}')"
    echo "Sertifikat olinmadi. $2 manzili shu server IP'siga (${ip:-server IP}) yo'naltirilganini tekshiring"
    echo "(DuckDNS'da «current ip»), keyin skriptni qayta ishga tushiring. Hozircha sayt http:// orqali ochiladi."
  fi
  return 0
}
# --- FUNKSIYALAR OXIRI ---

if [ "${CRM_SKIP_INSTALL:-0}" != "1" ]; then
  [ "$(id -u)" -eq 0 ] || { echo "Bu skriptni root sifatida ishga tushiring."; exit 1; }
  if ! command -v git >/dev/null 2>&1; then
    say "git o'rnatilmoqda"
    apt-get update -y && apt-get install -y git
  fi
  if ! command -v docker >/dev/null 2>&1; then
    say "Docker o'rnatilmoqda (2-3 daqiqa)"
    curl -fsSL https://get.docker.com | sh
  fi
fi

if [ -d "$DIR/.git" ]; then
  say "Dastur yangilanmoqda"
  git -C "$DIR" fetch origin "$BRANCH"
  git -C "$DIR" checkout "$BRANCH"
  git -C "$DIR" pull --ff-only origin "$BRANCH"
else
  say "Dastur yuklab olinmoqda"
  git clone -b "$BRANCH" "$REPO" "$DIR"
fi
cd "$DIR/TelegramCRM"

getvar() { grep -E "^$1=" .env | head -n1 | cut -d= -f2- || true; }

if [ -n "${ENV_FILE:-}" ]; then
  [ -f "$ENV_FILE" ] || { echo "ENV_FILE topilmadi: $ENV_FILE"; exit 1; }
  OLD_SECRET=""
  [ -f .env ] && OLD_SECRET="$(getvar SECRET_KEY)"
  say "Sizning .env faylingiz ishlatiladi: $ENV_FILE"
  cp "$ENV_FILE" .env
  RESET_ADMIN=1
fi
[ -f .env ] || cp .env.example .env
chmod 600 .env
# Windows'da saqlangan fayl: qator oxiridagi \r va boshidagi BOM belgisini olib tashlaymiz
sed -i 's/\r$//; 1s/^\xEF\xBB\xBF//' .env
setvar() {
  local key="$1" val="$2" esc
  esc=$(printf '%s' "$val" | sed -e 's/[\\|&]/\\&/g')
  if grep -qE "^$key=" .env; then
    sed -i "s|^$key=.*|$key=$esc|" .env
  else
    printf '%s=%s\n' "$key" "$val" >> .env
  fi
}

say "Sozlamalar (.env) yozilmoqda"
if [ -z "$(getvar SECRET_KEY)" ]; then
  setvar SECRET_KEY "${OLD_SECRET:-$(openssl rand -hex 32)}"
fi
NEW_PASS=""
if [ -z "$(getvar ADMIN_PASSWORD)" ]; then
  NEW_PASS=$(openssl rand -base64 18 | tr -dc 'A-Za-z0-9' | cut -c1-12)
  setvar ADMIN_PASSWORD "$NEW_PASS"
fi
[ -n "$(getvar ADMIN_LOGIN)" ] || setvar ADMIN_LOGIN admin
[ -n "$(getvar TIMEZONE)" ] || setvar TIMEZONE Asia/Tashkent
setvar TG_FAKE 0
[ -z "${BOT_TOKEN:-}" ] || setvar BOT_TOKEN "$BOT_TOKEN"
[ -z "${ADMIN_TG_ID:-}" ] || setvar ADMIN_TG_ID "$ADMIN_TG_ID"
[ -z "${TG_API_ID:-}" ] || setvar TG_API_ID "$TG_API_ID"
[ -z "${TG_API_HASH:-}" ] || setvar TG_API_HASH "$TG_API_HASH"
[ -z "${SITE_PRESET:-}" ] || setvar SITE_PRESET "$SITE_PRESET"
if [ -n "${DOMAIN:-}" ]; then
  DOMAIN="${DOMAIN#http://}"; DOMAIN="${DOMAIN#https://}"; DOMAIN="${DOMAIN%%/*}"
  setvar DOMAIN "$DOMAIN"
  setvar PUBLIC_URL "https://$DOMAIN"
  setvar COOKIE_SECURE 1
  setvar WWW_DOMAIN "${WWW_DOMAIN:-}"
elif [ -z "$(getvar DOMAIN)" ]; then
  setvar COOKIE_SECURE 0
fi

[ -n "$(getvar APP_PORT)" ] || setvar APP_PORT 8181
APP_PORT_VAL="$(getvar APP_PORT)"
MODE=caddy
HTTPS_OK=1

if [ "${CRM_SKIP_START:-0}" != "1" ]; then
  # 80/443-portni boshqa sayt (nginx, apache...) egallaganmi?
  OWNER="$(port_owner 80)"; [ -n "$OWNER" ] || OWNER="$(port_owner 443)"
  if [ -n "$OWNER" ] && ! docker compose --profile caddy ps --status running --services 2>/dev/null | grep -qx caddy; then
    case "$OWNER" in
      nginx) MODE=nginx ;;
      apache2|httpd) MODE=apache ;;
      *) MODE=other ;;
    esac
    say "80-port band: $OWNER. Serverdagi boshqa sayt saqlanadi, CRM u orqali ulanadi."
  fi
  if [ "$MODE" = caddy ]; then setvar COMPOSE_PROFILES caddy; else setvar COMPOSE_PROFILES ""; fi

  say "Sayt ishga tushirilmoqda (birinchi marta 3-6 daqiqa)"
  [ "$MODE" = caddy ] || docker compose --profile caddy rm -sf caddy >/dev/null 2>&1 || true
  docker compose up -d --build
  if [ "${RESET_ADMIN:-0}" = "1" ] && [ -n "$(getvar ADMIN_PASSWORD)" ]; then
    say "Admin paroli .env dagi parolga o'rnatilmoqda"
    for _ in $(seq 1 60); do
      docker compose exec -T app python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8000/api/health', timeout=3)" >/dev/null 2>&1 && break
      sleep 2
    done
    docker compose exec -T app python -m app.cli reset-password "$(getvar ADMIN_LOGIN)" "$(getvar ADMIN_PASSWORD)"
  fi

  DOMAIN_VAL="$(getvar DOMAIN)"
  if [ "$MODE" != caddy ] && [ -z "$DOMAIN_VAL" ]; then
    echo "Serverda boshqa sayt bor, shuning uchun CRM'ga domen kerak (masalan DOMAIN=mediamax.duckdns.org)."
    MODE=other
  fi
  case "$MODE" in
    nginx)
      say "Mavjud nginx'ga $DOMAIN_VAL qo'shilmoqda"
      if setup_nginx "$DOMAIN_VAL" "$APP_PORT_VAL"; then setup_https nginx "$DOMAIN_VAL"; else MODE=other; fi ;;
    apache)
      say "Mavjud apache'ga $DOMAIN_VAL qo'shilmoqda"
      if setup_apache "$DOMAIN_VAL" "$APP_PORT_VAL"; then setup_https apache "$DOMAIN_VAL"; else MODE=other; fi ;;
  esac
  if [ "$MODE" = nginx ] || [ "$MODE" = apache ]; then
    # Sertifikat bo'lsa https, bo'lmasa http — kirish (cookie) shunga mos bo'lishi kerak
    if [ "$HTTPS_OK" = 1 ]; then SCHEME=https; SECURE=1; else SCHEME=http; SECURE=0; fi
    if [ "$(getvar PUBLIC_URL)" != "$SCHEME://$DOMAIN_VAL" ] || [ "$(getvar COOKIE_SECURE)" != "$SECURE" ]; then
      setvar PUBLIC_URL "$SCHEME://$DOMAIN_VAL"
      setvar COOKIE_SECURE "$SECURE"
      docker compose up -d
    fi
  fi
  if [ "$MODE" = other ]; then
    say "DIQQAT: saytni avtomatik ulab bo'lmadi"
    echo "80/443-portni egallagan dastur: ${OWNER:-aniqlanmadi}"
    echo "CRM ishlab turibdi (serverning ichida, 127.0.0.1:$APP_PORT_VAL), lekin tashqaridan hali ochilmaydi."
    echo "Shu oynani rasmga olib yuboring — keyingi qadamni aytaman."
  fi
  docker compose ps
fi

URL="$(getvar PUBLIC_URL)"
if [ -z "$URL" ]; then
  URL="http://$(hostname -I 2>/dev/null | awk '{print $1}')"
elif [ "$HTTPS_OK" != 1 ]; then
  URL="http://$(getvar DOMAIN)"
fi
say "Tayyor"
echo "Sayt:   $URL"
case "$MODE" in
  nginx) echo "Ulanish: serverdagi mavjud nginx orqali (boshqa saytlar o'zgarmadi)" ;;
  apache) echo "Ulanish: serverdagi mavjud apache orqali (boshqa saytlar o'zgarmadi)" ;;
esac
echo "Login:  $(getvar ADMIN_LOGIN)"
if [ -n "$NEW_PASS" ]; then
  echo "Parol:  $NEW_PASS   ← yozib oling!"
elif [ "${RESET_ADMIN:-0}" = "1" ]; then
  echo "Parol:  .env faylingizdagi ADMIN_PASSWORD"
else
  echo "Parol:  avvalgisi (o'zgarmadi)"
fi
[ -z "$(getvar BOT_TOKEN)" ] || echo "Bot:    Telegram'da botingizga /balans deb yozib tekshiring"
[ -n "$(getvar TG_API_ID)" ] || echo "Eslatma: Telegram akkauntlarni ulash uchun my.telegram.org dan TG_API_ID va TG_API_HASH oling va skriptni ular bilan qayta ishga tushiring."
