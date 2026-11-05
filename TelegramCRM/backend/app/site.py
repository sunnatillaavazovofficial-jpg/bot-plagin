"""Ochiq bosh sahifa: Google/Yandex uchun matn, meta teglar, robots.txt va sitemap.xml.

Kompaniya ma'lumoti Sozlamalar → «Ochiq sahifa» bo'limida kiritiladi (settings jadvali). Nomi kiritilmagan
bo'lsa sayt qidiruv tizimlaridan butunlay yopiq turadi (noindex).
"""

from __future__ import annotations

import html
import json
import logging
import re
from pathlib import Path

from sqlalchemy.ext.asyncio import AsyncSession

from .settings_store import get_all, set_many

log = logging.getLogger(__name__)

LANGS = ("uz", "ru")
SITE_KEYS = (
    "site_name",
    "site_title_uz",
    "site_title_ru",
    "site_tagline_uz",
    "site_tagline_ru",
    "site_about_uz",
    "site_about_ru",
    "site_services_uz",
    "site_services_ru",
    "site_facts_uz",
    "site_facts_ru",
    "site_phone",
    "site_address_uz",
    "site_address_ru",
    "site_hours_uz",
    "site_hours_ru",
    "site_telegram",
    "site_instagram",
    "site_website",
)
VERIFY_KEYS = ("google_verification", "yandex_verification")
PRESETS_DIR = Path(__file__).resolve().parent / "presets"

# Saytning yopiq qismlari: qidiruv tizimlari kirmaydi
PRIVATE_PATHS = ("/api/", "/chats", "/orders", "/clients", "/finance", "/reports", "/settings", "/ws")

UI = {
    "uz": {
        "services": "Xizmatlar",
        "contacts": "Bog'lanish",
        "login": "Xodimlar uchun kirish",
    },
    "ru": {
        "services": "Услуги",
        "contacts": "Контакты",
        "login": "Вход для сотрудников",
    },
}


def lines(value: str) -> list[str]:
    return [line.strip(" \t-•") for line in (value or "").splitlines() if line.strip(" \t-•")]


def clean_verification(value: str) -> str:
    """Butun meta teg yopishtirilsa ham faqat content="..." qiymatini oladi."""
    value = (value or "").strip()
    match = re.search(r'content\s*=\s*["\']([^"\']+)["\']', value)
    if match:
        value = match.group(1)
    return re.sub(r"[^A-Za-z0-9_\-]", "", value)[:128]


def public_site(values: dict[str, str]) -> dict:
    """Kirishsiz ochiq ma'lumot (tasdiqlash kodlari berilmaydi)."""
    data: dict = {"configured": bool(values.get("site_name", "").strip())}
    for key in SITE_KEYS:
        name = key.removeprefix("site_")
        if key.startswith(("site_services", "site_facts")):
            data[name] = lines(values.get(key, ""))
        else:
            data[name] = values.get(key, "").strip()
    return data


def page_lang(path: str) -> str | None:
    """Ochiq sahifalar: «/» — o'zbekcha, «/ru» — ruscha. Boshqasi — CRM ichki sahifasi."""
    path = path.strip("/")
    if path == "":
        return "uz"
    if path == "ru":
        return "ru"
    return None


def _shorten(text: str, limit: int) -> str:
    text = re.sub(r"\s+", " ", text).strip()
    if len(text) <= limit:
        return text
    return text[: limit - 1].rsplit(" ", 1)[0].rstrip(" ,.;:—-") + "…"


def page_title(site: dict, lang: str) -> str:
    """Google/Yandex natijasidagi sarlavha: alohida kiritilgan bo'lsa o'sha, aks holda «Nom — shior» (≈60 belgi)."""
    own = _t(site, "title", lang)
    if own:
        return _shorten(own, 70)
    tagline = _t(site, "tagline", lang)
    return _shorten(f"{site['name']} — {tagline}", 60) if tagline else site["name"]


def _t(site: dict, field: str, lang: str) -> str:
    """Tanlangan tildagi qiymat; bo'lmasa ikkinchi tildagisi."""
    other = "ru" if lang == "uz" else "uz"
    return site.get(f"{field}_{lang}") or site.get(f"{field}_{other}") or ""


def _description(site: dict, lang: str) -> str:
    parts = [p.strip() for p in (_t(site, "tagline", lang), _t(site, "about", lang)) if p.strip()]
    parts = [p if p[-1] in ".!?…" else p + "." for p in parts]
    return _shorten(" ".join(parts), 160)


def render_index(template: str, values: dict[str, str], path: str, base_url: str) -> str:
    """index.html ni sahifa uchun to'ldiradi: meta teglar va JS'siz o'qiladigan matn."""
    site = public_site(values)
    lang = page_lang(path)
    esc = html.escape
    head: list[str] = []
    body = ""
    if site["configured"] and lang:
        name = site["name"]
        title = page_title(site, lang)
        desc = _description(site, lang)
        url = base_url + ("/ru" if lang == "ru" else "/")
        head += [
            f'<meta name="description" content="{esc(desc)}">',
            '<meta name="robots" content="index, follow">',
            f'<link rel="canonical" href="{esc(url)}">',
            f'<link rel="alternate" hreflang="uz" href="{esc(base_url)}/">',
            f'<link rel="alternate" hreflang="ru" href="{esc(base_url)}/ru">',
            f'<link rel="alternate" hreflang="x-default" href="{esc(base_url)}/">',
            '<meta property="og:type" content="website">',
            f'<meta property="og:title" content="{esc(title)}">',
            f'<meta property="og:description" content="{esc(desc)}">',
            f'<meta property="og:url" content="{esc(url)}">',
            f'<meta property="og:site_name" content="{esc(name)}">',
            f'<meta property="og:locale" content="{"ru_RU" if lang == "ru" else "uz_UZ"}">',
        ]
        same_as = [u for u in (site["telegram"], site["instagram"], site["website"]) if u.startswith("http")]
        ld = {
            "@context": "https://schema.org",
            "@type": "LocalBusiness",
            "name": name,
            "description": desc,
            "url": base_url + "/",
        }
        if site["phone"]:
            ld["telephone"] = site["phone"]
        address = _t(site, "address", lang)
        if address:
            ld["address"] = address
        if same_as:
            ld["sameAs"] = same_as
        ld_json = json.dumps(ld, ensure_ascii=False).replace("</", "<\\/")
        head.append(f'<script type="application/ld+json">{ld_json}</script>')
        body = _static_body(site, lang)
        template = re.sub(r"<title>.*?</title>", f"<title>{esc(title)}</title>", template, count=1, flags=re.S)
    else:
        head.append('<meta name="robots" content="noindex, nofollow">')
    # Tasdiqlash teglari — faqat bosh sahifada (Google/Yandex shu yerdan tekshiradi)
    if lang:
        google = clean_verification(values.get("google_verification", ""))
        yandex = clean_verification(values.get("yandex_verification", ""))
        if google:
            head.append(f'<meta name="google-site-verification" content="{esc(google)}">')
        if yandex:
            head.append(f'<meta name="yandex-verification" content="{esc(yandex)}">')
    out = template.replace('<html lang="uz">', f'<html lang="{lang or "uz"}">', 1)
    out = out.replace("<!--SEO_HEAD-->", "\n    ".join(head), 1)
    out = out.replace("<!--SEO_BODY-->", body, 1)
    return out


def _static_body(site: dict, lang: str) -> str:
    """React yuklanguncha (va JS'siz qidiruv robotlari uchun) ko'rinadigan oddiy matn."""
    esc = html.escape
    ui = UI[lang]
    parts = [
        '<main style="max-width:880px;margin:0 auto;padding:32px 16px;font-family:system-ui,sans-serif;line-height:1.6">',
        f"<h1>{esc(site['name'])}</h1>",
    ]
    tagline = _t(site, "tagline", lang)
    if tagline:
        parts.append(f"<p><strong>{esc(tagline)}</strong></p>")
    about = _t(site, "about", lang)
    if about:
        parts.append(f"<p>{esc(about)}</p>")
    facts = site.get(f"facts_{lang}") or site.get(f"facts_{'ru' if lang == 'uz' else 'uz'}") or []
    if facts:
        parts.append("<ul>" + "".join(f"<li>{esc(f)}</li>" for f in facts) + "</ul>")
    services = site.get(f"services_{lang}") or site.get(f"services_{'ru' if lang == 'uz' else 'uz'}") or []
    if services:
        parts.append(f"<h2>{ui['services']}</h2><ul>" + "".join(f"<li>{esc(s)}</li>" for s in services) + "</ul>")
    contacts = []
    if site["phone"]:
        tel = re.sub(r"[^\d+]", "", site["phone"])
        contacts.append(f'<a href="tel:{esc(tel)}">{esc(site["phone"])}</a>')
    for field in ("address", "hours"):
        value = _t(site, field, lang)
        if value:
            contacts.append(esc(value))
    for url in (site["telegram"], site["instagram"], site["website"]):
        if url.startswith("http"):
            contacts.append(f'<a href="{esc(url)}" rel="noopener">{esc(url)}</a>')
    if contacts:
        parts.append(f"<h2>{ui['contacts']}</h2><p>" + "<br>".join(contacts) + "</p>")
    other = "/ru" if lang == "uz" else "/"
    parts.append(f'<p><a href="{other}">{"Русский" if lang == "uz" else "O‘zbekcha"}</a></p>')
    parts.append("</main>")
    return "".join(parts)


def robots_txt(values: dict[str, str], base_url: str) -> str:
    if not values.get("site_name", "").strip():
        return "User-agent: *\nDisallow: /\n"
    rules = ["User-agent: *", "Allow: /$", "Allow: /ru$"]
    rules += [f"Disallow: {p}" for p in PRIVATE_PATHS]
    rules.append("")
    rules.append(f"Sitemap: {base_url}/sitemap.xml")
    return "\n".join(rules) + "\n"


def sitemap_xml(base_url: str) -> str:
    urls = []
    for lang, loc in (("uz", f"{base_url}/"), ("ru", f"{base_url}/ru")):
        alts = "".join(
            f'<xhtml:link rel="alternate" hreflang="{l}" href="{html.escape(u)}"/>'
            for l, u in (("uz", f"{base_url}/"), ("ru", f"{base_url}/ru"))
        )
        urls.append(f"<url><loc>{html.escape(loc)}</loc>{alts}<changefreq>monthly</changefreq></url>")
    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">'
        + "".join(urls)
        + "</urlset>\n"
    )


async def apply_preset(session: AsyncSession, name: str) -> int:
    """SITE_PRESET: tayyor matnni faqat bo'sh maydonlarga yozadi. Yozilgan maydonlar soni qaytadi."""
    if not re.fullmatch(r"[a-z0-9\-]+", name or ""):
        return 0
    path = PRESETS_DIR / f"{name}.json"
    if not path.is_file():
        log.warning("SITE_PRESET=%s topilmadi", name)
        return 0
    preset = json.loads(path.read_text(encoding="utf-8"))
    current = await get_all(session)
    updates = {}
    for key in SITE_KEYS:
        value = preset.get(key)
        if isinstance(value, list):
            value = "\n".join(value)
        if value and not current.get(key, "").strip():
            updates[key] = value
    if updates:
        await set_many(session, updates)
    return len(updates)
