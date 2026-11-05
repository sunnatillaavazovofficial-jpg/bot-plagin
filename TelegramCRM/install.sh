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
#   INSTALL_DIR   dastur papkasi (standart: ~/bot-plagin)
set -euo pipefail

REPO="https://github.com/sunnatillaavazovofficial-jpg/bot-plagin.git"
BRANCH="claude/blissful-brown-asw2bf"
DIR="${INSTALL_DIR:-$HOME/bot-plagin}"

say() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }

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

[ -f .env ] || cp .env.example .env
chmod 600 .env

getvar() { grep -E "^$1=" .env | head -n1 | cut -d= -f2- || true; }
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
[ -n "$(getvar SECRET_KEY)" ] || setvar SECRET_KEY "$(openssl rand -hex 32)"
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

if [ "${CRM_SKIP_START:-0}" != "1" ]; then
  say "Sayt ishga tushirilmoqda (birinchi marta 3-6 daqiqa)"
  docker compose up -d --build
  docker compose ps
fi

URL="$(getvar PUBLIC_URL)"
if [ -z "$URL" ]; then
  URL="http://$(hostname -I 2>/dev/null | awk '{print $1}')"
fi
say "Tayyor"
echo "Sayt:   $URL"
echo "Login:  $(getvar ADMIN_LOGIN)"
if [ -n "$NEW_PASS" ]; then
  echo "Parol:  $NEW_PASS   ← yozib oling!"
else
  echo "Parol:  avvalgisi (o'zgarmadi)"
fi
[ -z "$(getvar BOT_TOKEN)" ] || echo "Bot:    Telegram'da botingizga /balans deb yozib tekshiring"
[ -n "$(getvar TG_API_ID)" ] || echo "Eslatma: Telegram akkauntlarni ulash uchun my.telegram.org dan TG_API_ID va TG_API_HASH oling va skriptni ular bilan qayta ishga tushiring."
