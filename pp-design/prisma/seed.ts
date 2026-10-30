/**
 * Demo ma'lumotlar: dizaynerlar, otzivlar, ishlar va hamkorlar (sayt bo'sh ko'rinmasligi uchun).
 *   npm run db:seed
 * Hammasini keyin admin paneldan o'chirib, haqiqiy ma'lumotlar bilan almashtirish mumkin.
 * Bazada dizaynerlar bo'lsa, seed hech narsa qilmaydi.
 */
import { existsSync } from "node:fs";
import { mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { PrismaClient } from "@prisma/client";

if (existsSync(".env")) process.loadEnvFile(".env");

const db = new PrismaClient();
const UPLOAD_DIR = process.env.UPLOAD_DIR || "./uploads";

const PALETTES = [
  ["#38c8ff", "#7b61ff", "#0b1530"],
  ["#ff5ccd", "#7b61ff", "#140b2e"],
  ["#ffb547", "#ff5c5c", "#2a0f12"],
  ["#3ee6a8", "#38c8ff", "#062026"],
  ["#f9f871", "#ff9671", "#2b1a0c"],
  ["#c3a6ff", "#ff7ad9", "#1a1033"],
];

function rand(seed: number) {
  return () => {
    seed = (seed * 16807) % 2147483647;
    return (seed - 1) / 2147483646;
  };
}

function esc(text: string) {
  return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/'/g, "&apos;");
}

/** Abstrakt "ish" rasmi (SVG) */
function artwork(seed: number, title: string, kind: string, ratio: "wide" | "tall" | "square" = "wide") {
  const r = rand(seed * 977 + 13);
  const [a, b, bg] = PALETTES[seed % PALETTES.length];
  const W = 1200;
  const H = ratio === "wide" ? 900 : ratio === "tall" ? 1500 : 1200;
  const shapes = Array.from({ length: 7 }, (_, i) => {
    const x = Math.round(r() * W);
    const y = Math.round(r() * H);
    const s = Math.round(80 + r() * 320);
    const color = i % 2 ? a : b;
    const op = (0.25 + r() * 0.5).toFixed(2);
    return i % 3 === 0
      ? `<circle cx="${x}" cy="${y}" r="${s / 2}" fill="${color}" opacity="${op}"/>`
      : i % 3 === 1
        ? `<rect x="${x - s / 2}" y="${y - s / 2}" width="${s}" height="${s}" rx="${s / 5}" fill="${color}" opacity="${op}" transform="rotate(${Math.round(r() * 90)} ${x} ${y})"/>`
        : `<path d="M${x} ${y} q ${s} ${-s / 2} ${s * 1.4} ${s / 3}" stroke="${color}" stroke-width="${Math.round(10 + r() * 30)}" fill="none" stroke-linecap="round" opacity="${op}"/>`;
  }).join("");

  const cx = W / 2;
  const cy = H / 2;
  const mockups: Record<string, string> = {
    Brending: `<g transform="translate(${cx} ${cy - 40})"><circle r="150" fill="none" stroke="#fff" stroke-width="14"/><path d="M-60 70V-70h55a45 45 0 0 1 0 90h-55" fill="none" stroke="url(#g)" stroke-width="26" stroke-linecap="round" stroke-linejoin="round"/></g>`,
    Reklama: `<g transform="translate(${cx} ${cy - 30})"><rect x="-360" y="-130" width="720" height="260" rx="20" fill="${bg}" stroke="#fff" stroke-opacity=".6" stroke-width="6"/><text y="35" text-anchor="middle" font-family="Arial Black, Arial, sans-serif" font-size="120" font-weight="900" fill="url(#g)">${esc(title.split(" ")[0].toUpperCase())}</text><rect x="-8" y="130" width="16" height="200" fill="#fff" opacity=".5"/></g>`,
    Poligrafiya: `<g transform="translate(${cx} ${cy})"><rect x="-260" y="-330" width="380" height="540" rx="12" fill="#f4efe6" transform="rotate(-8)"/><rect x="-120" y="-300" width="380" height="540" rx="12" fill="#fff" transform="rotate(6)"/><rect x="-60" y="-200" width="260" height="160" rx="8" fill="url(#g)" transform="rotate(6)"/><g transform="rotate(6)" fill="#1b1712" opacity=".75"><rect x="-60" y="0" width="260" height="14"/><rect x="-60" y="30" width="200" height="14"/><rect x="-60" y="60" width="240" height="14"/></g></g>`,
    Veb: `<g transform="translate(${cx} ${cy})"><rect x="-420" y="-260" width="840" height="520" rx="26" fill="#0d1328" stroke="#fff" stroke-opacity=".35" stroke-width="4"/><rect x="-420" y="-260" width="840" height="56" rx="26" fill="#ffffff" opacity=".08"/><rect x="-370" y="-150" width="380" height="40" rx="10" fill="url(#g)"/><rect x="-370" y="-90" width="300" height="18" rx="9" fill="#fff" opacity=".4"/><rect x="-370" y="-55" width="260" height="18" rx="9" fill="#fff" opacity=".25"/><rect x="60" y="-160" width="310" height="300" rx="20" fill="url(#g)" opacity=".85"/><rect x="-370" y="40" width="130" height="44" rx="22" fill="${a}"/></g>`,
    SMM: `<g transform="translate(${cx} ${cy})"><rect x="-200" y="-380" width="400" height="760" rx="56" fill="#0d1328" stroke="#fff" stroke-opacity=".5" stroke-width="6"/><rect x="-170" y="-300" width="340" height="340" rx="18" fill="url(#g)"/><circle cx="-130" cy="100" r="26" fill="${a}"/><rect x="-90" y="88" width="180" height="22" rx="11" fill="#fff" opacity=".5"/><rect x="-170" y="160" width="340" height="16" rx="8" fill="#fff" opacity=".3"/><rect x="-170" y="195" width="260" height="16" rx="8" fill="#fff" opacity=".2"/></g>`,
  };
  const mock = mockups[kind] ?? mockups.Brending;
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" width="${W}" height="${H}">
<defs><linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${bg}"/><stop offset="1" stop-color="#050b1c"/></linearGradient>
<linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${a}"/><stop offset="1" stop-color="${b}"/></linearGradient></defs>
<rect width="${W}" height="${H}" fill="url(#bg)"/>${shapes}${mock}
</svg>`;
}

function partnerLogo(name: string, i: number) {
  const [a, b] = PALETTES[i % PALETTES.length];
  const marks = [
    `<circle cx="40" cy="40" r="26" fill="none" stroke="url(#g)" stroke-width="9"/>`,
    `<rect x="14" y="14" width="52" height="52" rx="14" fill="url(#g)"/>`,
    `<path d="M14 66 40 14l26 52Z" fill="url(#g)"/>`,
    `<path d="M14 40a26 26 0 1 0 52 0" fill="none" stroke="url(#g)" stroke-width="10" stroke-linecap="round"/><circle cx="40" cy="22" r="8" fill="${a}"/>`,
  ];
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 360 80" width="360" height="80">
<defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="${a}"/><stop offset="1" stop-color="${b}"/></linearGradient></defs>
${marks[i % marks.length]}<text x="84" y="52" font-family="Arial, Helvetica, sans-serif" font-size="32" font-weight="700" fill="#ffffff">${esc(name)}</text></svg>`;
}

async function saveSvg(name: string, svg: string) {
  const dir = path.resolve(UPLOAD_DIR, "seed");
  await mkdir(dir, { recursive: true });
  await writeFile(path.join(dir, name), svg);
  return `/uploads/seed/${name}`;
}

const DESIGNERS = [
  {
    username: "aziza-karimova",
    fullName: "Aziza Karimova",
    specialty: "Brending va logotip dizayneri",
    skills: ["Logotip", "Brendbuk", "Illustrator", "Tipografika"],
    years: 7,
    bio: "Brendlarga yuz va xarakter beraman. Kichik biznesdan yirik kompaniyalargacha 150+ logotip va firma uslubi yaratganman.",
    kinds: ["Brending", "Poligrafiya", "Brending"],
    prices: [
      { title: "Logotip", price: "1 500 000 so'mdan" },
      { title: "Firma uslubi (brendbuk)", price: "4 000 000 so'mdan" },
    ],
  },
  {
    username: "jasur-toshmatov",
    fullName: "Jasur Toshmatov",
    specialty: "Tashqi reklama dizayneri",
    skills: ["Hajmli harflar", "3D vizual", "CorelDRAW"],
    years: 9,
    bio: "Vivesekalar, lightbox va hajmli harflar uchun maket va real vizualizatsiya. Ishlab chiqarish texnologiyasini yaxshi bilaman.",
    kinds: ["Reklama", "Reklama", "Brending"],
    prices: [
      { title: "Vivesko maketi", price: "500 000 so'mdan" },
      { title: "3D vizualizatsiya", price: "800 000 so'mdan" },
    ],
  },
  {
    username: "madina-rahimova",
    fullName: "Madina Rahimova",
    specialty: "UI/UX dizayner",
    skills: ["Figma", "Veb-sayt", "Mobil ilova"],
    years: 5,
    bio: "Foydalanuvchi uchun qulay va chiroyli interfeyslar: landing, internet-do'kon, mobil ilova va Telegram botlar.",
    kinds: ["Veb", "Veb", "SMM"],
    prices: [
      { title: "Landing sahifa dizayni", price: "3 000 000 so'mdan" },
      { title: "Mobil ilova (1 ekran)", price: "400 000 so'mdan" },
    ],
  },
  {
    username: "otabek-yusupov",
    fullName: "Otabek Yusupov",
    specialty: "SMM dizayner",
    skills: ["Instagram", "Reels cover", "Photoshop"],
    years: 4,
    bio: "Ijtimoiy tarmoqlar uchun sotadigan kreativlar: post, story, karusel va reklama bannerlari.",
    kinds: ["SMM", "SMM", "Reklama"],
    prices: [{ title: "Oylik SMM paket (30 post)", price: "2 500 000 so'm" }],
  },
  {
    username: "dilnoza-ergasheva",
    fullName: "Dilnoza Ergasheva",
    specialty: "Illyustrator va qadoq dizayneri",
    skills: ["Illyustratsiya", "Qadoq", "Procreate"],
    years: 6,
    bio: "Qadoq va yorliqlar uchun qo'lda chizilgan illyustratsiyalar. Milliy naqshlarni zamonaviy uslubda qo'llayman.",
    kinds: ["Poligrafiya", "Brending", "Poligrafiya"],
    prices: [{ title: "Qadoq dizayni", price: "2 000 000 so'mdan" }],
  },
  {
    username: "sardor-aliyev",
    fullName: "Sardor Aliyev",
    specialty: "AI dizayner",
    skills: ["Midjourney", "Konsept", "Vizualizatsiya"],
    years: 3,
    bio: "Sun'iy intellekt yordamida tezkor konseptlar, reklama vizuallari va moodboardlar. G'oyadan natijagacha — bir necha soatda.",
    kinds: ["Reklama", "Veb", "SMM"],
    prices: [{ title: "AI konsept (5 variant)", price: "700 000 so'm" }],
  },
];

const REVIEWS = [
  "Juda tez va sifatli ishladi, barcha istaklarimizni inobatga oldi. Rahmat!",
  "Professional yondashuv. Logotip hammaga yoqdi, albatta yana murojaat qilamiz.",
  "Muddatida topshirdi, tahrirlarni ham tez qildi. Tavsiya qilaman.",
  "G'oyani juda yaxshi tushundi va kutganimizdan ham zo'r natija berdi.",
  "Narx va sifat mutanosib. Muloqot qilish oson.",
];

const PROJECTS = [
  ["Mevazor — brend identifikatsiyasi", "Brending", "Meva yetkazib berish xizmati uchun logotip va firma uslubi."],
  ["Lumo Cafe — vivesko", "Reklama", "Hajmli yorug'lik harflari va kechki ko'rinish vizualizatsiyasi."],
  ["Zarafshon Tex — katalog", "Poligrafiya", "48 sahifali mahsulot katalogi va buklet."],
  ["Orbita Media — veb-sayt", "Veb", "Media agentlik uchun landing va admin panel dizayni."],
  ["Bahorgul — Instagram", "SMM", "Gul do'koni uchun oylik kontent va reklama kreativlari."],
  ["Qora Tog' Coffee — qadoq", "Poligrafiya", "Qahva qadoqlari va yorliqlar seriyasi."],
  ["Tosh Sport — reklama kampaniyasi", "Reklama", "Bilbord va tranzit reklama uchun kreativ."],
  ["Navo Music — brending", "Brending", "Musiqa studiyasi uchun logotip va ijtimoiy tarmoq shablonlari."],
  ["Sayyor Travel — mobil ilova", "Veb", "Sayohat agentligi uchun mobil ilova interfeysi."],
] as const;

const PARTNERS = ["Mevazor", "Lumo Cafe", "Zarafshon Tex", "Orbita Media", "Bahorgul", "Qora Tog'", "Tosh Sport", "Navo Music"];

async function main() {
  if ((await db.designer.count()) > 0) {
    console.log("Bazada dizaynerlar bor — demo ma'lumotlar qo'shilmadi.");
    return;
  }

  const clients = await Promise.all(
    ["Bekzod", "Nilufar", "Rustam", "Gulnoza", "Shoxrux"].map((name, i) =>
      db.user.create({ data: { role: "CLIENT", name, phone: `+99890111220${i}` } }),
    ),
  );

  for (const [i, d] of DESIGNERS.entries()) {
    const user = await db.user.create({ data: { role: "DESIGNER", name: d.fullName, telegramId: BigInt(900000100 + i), telegramUsername: d.username.replace("-", "_") } });
    const designer = await db.designer.create({
      data: {
        userId: user.id,
        username: d.username,
        fullName: d.fullName,
        specialty: d.specialty,
        bio: d.bio,
        experienceYears: d.years,
        experience: `${d.years} yildan beri dizayn sohasida. Agentliklarda va mustaqil (freelance) ishlaganman.`,
        skills: d.skills,
        prices: d.prices,
        socials: { telegram: d.username.replace("-", "_"), instagram: d.username.replace("-", ".") },
        status: "APPROVED",
        approvedAt: new Date(Date.now() - (10 - i) * 86400000),
      },
    });
    for (const [k, kind] of d.kinds.entries()) {
      const url = await saveSvg(`d${i}-${k}.svg`, artwork(i * 3 + k + 1, `${d.fullName}`, kind, k === 1 ? "square" : "wide"));
      await db.portfolioItem.create({ data: { designerId: designer.id, title: `${kind} loyihasi #${k + 1}`, imageUrl: url, order: k } });
    }
    const reviewCount = 2 + (i % 3);
    for (let k = 0; k < reviewCount; k++) {
      await db.review.create({
        data: { designerId: designer.id, clientId: clients[(i + k) % clients.length].id, rating: k === 2 ? 4 : 5, text: REVIEWS[(i + k) % REVIEWS.length] },
      });
    }
  }

  // Tasdiqlanishini kutayotgan dizayner (admin panelni sinash uchun)
  const pendingUser = await db.user.create({ data: { role: "DESIGNER", name: "Kamola Nazarova", telegramId: BigInt(900000199), telegramUsername: "kamola_n" } });
  await db.designer.create({
    data: { userId: pendingUser.id, username: "kamola-nazarova", fullName: "Kamola Nazarova", specialty: "Grafik dizayner", status: "PENDING" },
  });

  for (const [i, [title, category, description]] of PROJECTS.entries()) {
    const url = await saveSvg(`p${i}.svg`, artwork(i + 20, title, category, i % 4 === 1 ? "tall" : i % 4 === 3 ? "square" : "wide"));
    await db.project.create({ data: { title, category, description, imageUrl: url, order: i } });
  }

  for (const [i, name] of PARTNERS.entries()) {
    const url = await saveSvg(`partner${i}.svg`, partnerLogo(name, i));
    await db.partner.create({ data: { name, logoUrl: url, order: i } });
  }

  console.log("✅ Demo ma'lumotlar qo'shildi");
}

main()
  .catch((error) => {
    console.error(error);
    process.exit(1);
  })
  .finally(() => db.$disconnect());
