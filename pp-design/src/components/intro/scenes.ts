/**
 * Intro animatsiyasi: dizayn tarixi 6 bosqichda (~10.5 soniya), to'liq Canvas 2D da chiziladi.
 * Har bir sahna p ∈ [0..1] (sahna ichidagi progress) bo'yicha chiziladi, sahnalar bir-biriga silliq o'tadi.
 */

export type SceneInfo = { key: string; era: string; title: string; duration: number };

export const SCENES: SceneInfo[] = [
  { key: "cave", era: "Miloddan avvalgi 40 000 yil", title: "G'or devoridagi rasmlar", duration: 1.75 },
  { key: "egypt", era: "Miloddan avvalgi 3000 yil", title: "Qadimgi Misr ierogliflari", duration: 1.65 },
  { key: "renaissance", era: "XV–XVI asrlar", title: "Uyg'onish davri san'ati", duration: 1.65 },
  { key: "print", era: "1450 — 1920", title: "Kitob bosish va plakatlar", duration: 1.65 },
  { key: "digital", era: "1990-yillar", title: "Kompyuter grafikasi", duration: 1.65 },
  { key: "ai", era: "Bugun", title: "Sun'iy intellekt dizayni", duration: 2.2 },
];

export const FADE = 0.35;
export const TOTAL_DURATION = SCENES.reduce((sum, s) => sum + s.duration, 0);

export function sceneStart(index: number): number {
  let t = 0;
  for (let i = 0; i < index; i++) t += SCENES[i].duration;
  return t;
}

// ---------------- Yordamchi funksiyalar ----------------

type Ctx = CanvasRenderingContext2D;
type Pt = [number, number];

const clamp = (v: number, a = 0, b = 1) => Math.min(b, Math.max(a, v));
const lerp = (a: number, b: number, t: number) => a + (b - a) * t;
const easeOut = (t: number) => 1 - Math.pow(1 - clamp(t), 3);
const easeInOut = (t: number) => {
  const x = clamp(t);
  return x < 0.5 ? 4 * x * x * x : 1 - Math.pow(-2 * x + 2, 3) / 2;
};
/** p ni [a..b] oralig'ida 0..1 ga aylantiradi */
const seg = (p: number, a: number, b: number) => clamp((p - a) / (b - a));

function mulberry32(seed: number) {
  return () => {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** Catmull-Rom orqali silliq egri chiziq nuqtalari */
function spline(points: Pt[], steps = 10, closed = false): Pt[] {
  const pts = closed ? [points[points.length - 1], ...points, points[0], points[1]] : [points[0], ...points, points[points.length - 1]];
  const out: Pt[] = [];
  for (let i = 1; i < pts.length - 2; i++) {
    const [p0, p1, p2, p3] = [pts[i - 1], pts[i], pts[i + 1], pts[i + 2]];
    for (let s = 0; s < steps; s++) {
      const t = s / steps;
      const t2 = t * t;
      const t3 = t2 * t;
      out.push([
        0.5 * (2 * p1[0] + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3),
        0.5 * (2 * p1[1] + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3),
      ]);
    }
  }
  out.push(closed ? points[0] : points[points.length - 1]);
  return out;
}

/** Chiziqning faqat `frac` qismini chizadi (qo'lda chizilayotgandek effekt). Uchidagi nuqtani qaytaradi. */
function strokePartial(ctx: Ctx, pts: Pt[], frac: number): Pt | null {
  if (frac <= 0 || pts.length < 2) return null;
  const total = pts.length - 1;
  const upto = frac * total;
  const whole = Math.floor(upto);
  ctx.beginPath();
  ctx.moveTo(pts[0][0], pts[0][1]);
  for (let i = 1; i <= whole && i < pts.length; i++) ctx.lineTo(pts[i][0], pts[i][1]);
  let tip: Pt = pts[Math.min(whole, total)];
  if (whole < total) {
    const r = upto - whole;
    tip = [lerp(pts[whole][0], pts[whole + 1][0], r), lerp(pts[whole][1], pts[whole + 1][1], r)];
    ctx.lineTo(tip[0], tip[1]);
  }
  ctx.stroke();
  return tip;
}

function arcPts(cx: number, cy: number, r: number, a0: number, a1: number, n = 48): Pt[] {
  const out: Pt[] = [];
  for (let i = 0; i <= n; i++) {
    const a = lerp(a0, a1, i / n);
    out.push([cx + Math.cos(a) * r, cy + Math.sin(a) * r]);
  }
  return out;
}

const line = (a: Pt, b: Pt, n = 12): Pt[] => Array.from({ length: n + 1 }, (_, i) => [lerp(a[0], b[0], i / n), lerp(a[1], b[1], i / n)] as Pt);

/** Bir nechta chiziqni ketma-ket chizish: umumiy progress ularga bo'linadi */
function strokeSequence(ctx: Ctx, paths: Pt[][], p: number): Pt | null {
  const lengths = paths.map((path) => path.length);
  const total = lengths.reduce((a, b) => a + b, 0);
  let budget = p * total;
  let tip: Pt | null = null;
  for (let i = 0; i < paths.length; i++) {
    if (budget <= 0) break;
    const frac = Math.min(1, budget / lengths[i]);
    tip = strokePartial(ctx, paths[i], frac) ?? tip;
    budget -= lengths[i];
  }
  return tip;
}

// ---------------- Teksturalar (bir marta yaratiladi) ----------------

function makeTexture(size: number, seed: number, base: [string, string], blots: string[], count: number, extra?: (ctx: Ctx, rnd: () => number) => void) {
  const canvas = document.createElement("canvas");
  canvas.width = canvas.height = size;
  const ctx = canvas.getContext("2d")!;
  const rnd = mulberry32(seed);
  const g = ctx.createRadialGradient(size / 2, size / 2, size * 0.1, size / 2, size / 2, size * 0.75);
  g.addColorStop(0, base[0]);
  g.addColorStop(1, base[1]);
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, size, size);
  for (let i = 0; i < count; i++) {
    ctx.globalAlpha = 0.04 + rnd() * 0.1;
    ctx.fillStyle = blots[Math.floor(rnd() * blots.length)];
    ctx.beginPath();
    ctx.ellipse(rnd() * size, rnd() * size, 8 + rnd() * size * 0.12, 6 + rnd() * size * 0.08, rnd() * Math.PI, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.globalAlpha = 1;
  extra?.(ctx, rnd);
  // mayda don (grain)
  const img = ctx.getImageData(0, 0, size, size);
  for (let i = 0; i < img.data.length; i += 4) {
    const n = (rnd() - 0.5) * 22;
    img.data[i] += n;
    img.data[i + 1] += n;
    img.data[i + 2] += n;
  }
  ctx.putImageData(img, 0, 0);
  return canvas;
}

export type Textures = Record<"rock" | "sand" | "parchment" | "paper", HTMLCanvasElement>;

export function createTextures(small: boolean): Textures {
  const size = small ? 512 : 900;
  return {
    rock: makeTexture(size, 11, ["#7a4a28", "#1c0f07"], ["#3a2212", "#a0683b", "#5a3519", "#2a170b", "#c4874e"], 260, (ctx, rnd) => {
      ctx.strokeStyle = "rgba(20,10,5,0.35)";
      for (let i = 0; i < 14; i++) {
        ctx.lineWidth = 0.6 + rnd() * 1.6;
        ctx.beginPath();
        let x = rnd() * size;
        let y = rnd() * size;
        ctx.moveTo(x, y);
        for (let k = 0; k < 8; k++) {
          x += (rnd() - 0.5) * 70;
          y += (rnd() - 0.3) * 50;
          ctx.lineTo(x, y);
        }
        ctx.stroke();
      }
    }),
    sand: makeTexture(size, 23, ["#dcb57a", "#94672f"], ["#c99a5c", "#e8c995", "#a8773d", "#f0d6a8"], 220),
    parchment: makeTexture(size, 37, ["#efdfba", "#b8935a"], ["#d9c193", "#c7a46c", "#f5e8c8", "#a98450"], 200),
    paper: makeTexture(size, 41, ["#f3ecdc", "#d9cfb8"], ["#e6dcc6", "#fbf6ea", "#d2c6aa"], 160),
  };
}

// ---------------- Kadr konteksti ----------------

export type Frame = {
  ctx: Ctx;
  w: number; // CSS piksel
  h: number;
  dpr: number;
  S: number; // dizayn birligi → CSS piksel
  t: number; // umumiy vaqt (s)
  p: number; // sahna progressi
  tex: Textures;
  colors: { brand: string; brand2: string; brand3: string };
  fonts: { display: string; sans: string };
  brandName: string;
};

/** Ekran koordinatalari (CSS piksel) */
function screen(f: Frame) {
  f.ctx.setTransform(f.dpr, 0, 0, f.dpr, 0, 0);
}

/** Dizayn koordinatalari: markaz (0,0), taxminan x ∈ [-550..550], y ∈ [-380..380]. Sahna davomida sekin yaqinlashadi. */
function art(f: Frame, zoom = 1) {
  const k = f.S * zoom * f.dpr;
  f.ctx.setTransform(k, 0, 0, k, (f.w / 2) * f.dpr, (f.h / 2) * f.dpr);
}

function cover(f: Frame, img: HTMLCanvasElement) {
  screen(f);
  const scale = Math.max(f.w / img.width, f.h / img.height);
  const dw = img.width * scale;
  const dh = img.height * scale;
  f.ctx.drawImage(img, (f.w - dw) / 2, (f.h - dh) / 2, dw, dh);
}

function vignette(f: Frame, strength = 0.75, color = "0,0,0") {
  screen(f);
  const g = f.ctx.createRadialGradient(f.w / 2, f.h / 2, Math.min(f.w, f.h) * 0.25, f.w / 2, f.h / 2, Math.max(f.w, f.h) * 0.75);
  g.addColorStop(0, `rgba(${color},0)`);
  g.addColorStop(1, `rgba(${color},${strength})`);
  f.ctx.fillStyle = g;
  f.ctx.fillRect(0, 0, f.w, f.h);
}

// ================= 1. G'OR RASMLARI =================

const BISON_BODY = spline(
  [
    [-268, 30], [-262, 58], [-238, 92], [-210, 112], [-175, 108], [-120, 100], [-20, 108], [80, 100], [140, 88],
    [190, 70], [222, 20], [205, -30], [130, -52], [40, -62], [-40, -110], [-110, -150], [-170, -128], [-215, -70], [-250, -20],
  ],
  8,
  true,
);
const BISON_LEGS = [
  spline([[-170, 108], [-178, 170], [-170, 215]], 8),
  spline([[-120, 104], [-110, 165], [-122, 210]], 8),
  spline([[130, 90], [150, 150], [140, 208]], 8),
  spline([[180, 72], [205, 140], [214, 200]], 8),
];
const BISON_EXTRA = [
  spline([[-222, -62], [-250, -96], [-236, -132]], 8),
  spline([[-198, -84], [-212, -112], [-200, -136]], 8),
  spline([[215, -10], [250, 20], [248, 72]], 8),
];
const HUNTERS = [0, 1].map((k) => {
  const x = 330 + k * 95;
  const y = -175 + k * 30;
  return [
    arcPts(x, y - 34, 11, 0, Math.PI * 2, 16),
    line([x, y - 22], [x - 6, y + 18]),
    line([x - 6, y + 18], [x - 26, y + 52]),
    line([x - 6, y + 18], [x + 16, y + 50]),
    line([x - 2, y - 8], [x + 30, y - 2]),
    line([x - 2, y - 8], [x - 30, y + 4]),
    line([x + 52, y - 40], [x - 40, y + 30]),
  ];
});
const SUN_SPIRAL: Pt[] = Array.from({ length: 90 }, (_, i) => {
  const a = i * 0.21;
  const r = 4 + i * 0.75;
  return [-400 + Math.cos(a) * r, -230 + Math.sin(a) * r];
});

/** "Negativ" qo'l izi: atrofiga bo'yoq purkalgan, qo'l joyi och qoladi — g'or san'atining mashhur usuli */
function handprint(ctx: Ctx, x: number, y: number, scale: number, rot: number, rnd: () => number, k: number) {
  ctx.save();
  ctx.translate(x, y);
  ctx.rotate(rot);
  ctx.scale(scale, scale);
  const hand = () => {
    ctx.beginPath();
    ctx.ellipse(0, 12, 25, 29, 0, 0, Math.PI * 2);
    const fingers: [number, number, number, number][] = [
      [-30, 4, -0.95, 17],
      [-15, -30, -0.22, 21],
      [0, -36, 0, 24],
      [15, -31, 0.18, 22],
      [27, -17, 0.45, 17],
    ];
    for (const [fx, fy, fr, len] of fingers) {
      ctx.moveTo(fx + Math.cos(fr) * 7, fy);
      ctx.ellipse(fx, fy, 7, len, fr, 0, Math.PI * 2);
    }
  };
  // purkalgan bo'yoq halqasi
  for (let i = 0; i < 160; i++) {
    const a = rnd() * Math.PI * 2;
    const r = 30 + Math.pow(rnd(), 0.6) * 55;
    ctx.globalAlpha = (0.12 + rnd() * 0.3) * k;
    ctx.beginPath();
    ctx.arc(Math.cos(a) * r, Math.sin(a) * r - 6, 1.5 + rnd() * 4, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.globalAlpha = 0.5 * k;
  ctx.beginPath();
  ctx.ellipse(0, -4, 72, 80, 0, 0, Math.PI * 2);
  ctx.fill();
  // qo'l joyi — tosh rangida
  ctx.globalAlpha = 0.92 * k;
  ctx.fillStyle = "#9a6a42";
  hand();
  ctx.fill();
  ctx.restore();
  ctx.globalAlpha = 1;
}

function drawCave(f: Frame) {
  const { ctx, p, t } = f;
  cover(f, f.tex.rock);

  art(f, 1 + p * 0.05);
  ctx.lineCap = "round";
  ctx.lineJoin = "round";

  // nuqtalar qatori
  ctx.fillStyle = "#b5462a";
  const dots = Math.floor(seg(p, 0.1, 0.9) * 16);
  for (let i = 0; i < dots; i++) {
    ctx.beginPath();
    ctx.arc(-160 + i * 26, 245 + Math.sin(i) * 4, 6, 0, Math.PI * 2);
    ctx.fill();
  }

  // quyosh spirali
  ctx.strokeStyle = "rgba(196,92,42,0.9)";
  ctx.lineWidth = 7;
  strokePartial(ctx, SUN_SPIRAL, seg(p, 0.05, 0.6));

  // ovchilar (ko'mir bilan)
  ctx.strokeStyle = "rgba(25,14,8,0.85)";
  ctx.lineWidth = 6;
  for (const hunter of HUNTERS) strokeSequence(ctx, hunter, seg(p, 0.25, 0.85));

  // bizon — oxra rang bilan
  ctx.shadowColor = "rgba(120,40,10,0.6)";
  ctx.shadowBlur = 6;
  const fill = seg(p, 0.5, 0.9);
  if (fill > 0) {
    ctx.save();
    ctx.globalAlpha = fill * 0.55;
    ctx.fillStyle = "#b8532c";
    ctx.beginPath();
    BISON_BODY.forEach(([x, y], i) => (i ? ctx.lineTo(x, y) : ctx.moveTo(x, y)));
    ctx.fill();
    ctx.globalAlpha = fill * 0.6;
    const g = ctx.createRadialGradient(-120, -95, 10, -120, -95, 150);
    g.addColorStop(0, "#2a160b");
    g.addColorStop(1, "rgba(42,22,11,0)");
    ctx.fillStyle = g;
    ctx.fill();
    ctx.restore();
  }
  ctx.strokeStyle = "#2b170b";
  ctx.lineWidth = 11;
  const tip = strokePartial(ctx, BISON_BODY, seg(p, 0.0, 0.5));
  ctx.lineWidth = 9;
  BISON_LEGS.forEach((leg, i) => strokePartial(ctx, leg, seg(p, 0.35 + i * 0.05, 0.6 + i * 0.05)));
  BISON_EXTRA.forEach((part) => strokePartial(ctx, part, seg(p, 0.45, 0.7)));
  if (p > 0.5) {
    ctx.fillStyle = "#1a0d06";
    ctx.beginPath();
    ctx.arc(-232, -10, 6, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.shadowBlur = 0;

  // chizayotgan "barmoq" uchidagi uchqun
  if (tip && p < 0.5) {
    ctx.fillStyle = "rgba(255,180,90,0.9)";
    ctx.beginPath();
    ctx.arc(tip[0], tip[1], 7, 0, Math.PI * 2);
    ctx.fill();
  }

  // qo'l izlari
  const rnd = mulberry32(5);
  const hands: [number, number, number, number, string][] = [
    [-430, 150, 0.55, -0.3, "#a33a22"],
    [420, 130, 0.62, 0.25, "#c25a2c"],
    [455, -30, 0.48, 0.6, "#8f2f1c"],
  ];
  hands.forEach(([x, y, delay, rot, color]) => {
    const k = seg(p, delay, delay + 0.08);
    if (k <= 0) return;
    ctx.fillStyle = color;
    handprint(ctx, x, y, 1.05 * (1.2 - easeOut(k) * 0.2), rot, rnd, easeOut(k));
  });

  // mash'al yorug'ligi (lipillaydi)
  screen(f);
  const flicker = 0.85 + Math.sin(t * 23) * 0.05 + Math.sin(t * 37) * 0.04;
  const g = ctx.createRadialGradient(f.w * 0.42, f.h * 0.55, 0, f.w * 0.42, f.h * 0.55, Math.max(f.w, f.h) * 0.62 * flicker);
  g.addColorStop(0, "rgba(255,160,70,0.20)");
  g.addColorStop(0.55, "rgba(120,50,10,0.05)");
  g.addColorStop(1, "rgba(0,0,0,0.78)");
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, f.w, f.h);
}

// ================= 2. MISR =================

type Glyph = (ctx: Ctx) => void;
const GLYPHS: Glyph[] = [
  // ankh
  (c) => {
    c.beginPath();
    c.ellipse(0, -16, 11, 15, 0, 0, Math.PI * 2);
    c.moveTo(0, -1);
    c.lineTo(0, 34);
    c.moveTo(-22, 2);
    c.lineTo(22, 2);
    c.stroke();
  },
  // qush
  (c) => {
    c.beginPath();
    c.ellipse(-2, 6, 19, 11, -0.25, 0, Math.PI * 2);
    c.fill();
    c.beginPath();
    c.arc(16, -14, 6, 0, Math.PI * 2);
    c.fill();
    c.beginPath();
    c.moveTo(20, -14);
    c.quadraticCurveTo(34, -8, 36, 4);
    c.moveTo(-18, 8);
    c.lineTo(-32, 18);
    c.moveTo(-2, 16);
    c.lineTo(-6, 34);
    c.moveTo(6, 15);
    c.lineTo(8, 34);
    c.stroke();
  },
  // suv (zigzag)
  (c) => {
    c.beginPath();
    for (let row = 0; row < 2; row++) {
      c.moveTo(-28, -10 + row * 18);
      for (let i = 0; i <= 8; i++) c.lineTo(-28 + i * 7, -10 + row * 18 + (i % 2 ? -7 : 0));
    }
    c.stroke();
  },
  // quyosh diski
  (c) => {
    c.beginPath();
    c.arc(0, 0, 17, 0, Math.PI * 2);
    c.fill();
    c.beginPath();
    c.arc(0, 0, 25, 0, Math.PI * 2);
    c.stroke();
  },
  // qamish bargi
  (c) => {
    c.beginPath();
    c.moveTo(0, 32);
    c.quadraticCurveTo(-16, 0, 0, -32);
    c.quadraticCurveTo(14, 0, 0, 32);
    c.fill();
    c.beginPath();
    c.moveTo(0, 32);
    c.lineTo(0, 38);
    c.stroke();
  },
  // skarabey
  (c) => {
    c.beginPath();
    c.ellipse(0, 4, 13, 18, 0, 0, Math.PI * 2);
    c.fill();
    c.beginPath();
    c.arc(0, -18, 7, 0, Math.PI * 2);
    c.fill();
    c.beginPath();
    for (const s of [-1, 1]) {
      c.moveTo(s * 12, -4);
      c.lineTo(s * 26, -14);
      c.moveTo(s * 13, 6);
      c.lineTo(s * 28, 6);
      c.moveTo(s * 12, 14);
      c.lineTo(s * 24, 28);
    }
    c.stroke();
  },
  // piramida
  (c) => {
    c.beginPath();
    c.moveTo(-28, 26);
    c.lineTo(0, -26);
    c.lineTo(28, 26);
    c.closePath();
    c.stroke();
    c.beginPath();
    c.moveTo(0, -26);
    c.lineTo(6, 26);
    c.stroke();
  },
  // pat (Maat)
  (c) => {
    c.beginPath();
    c.moveTo(0, 34);
    c.bezierCurveTo(-4, 0, -14, -20, 4, -34);
    c.bezierCurveTo(14, -16, 10, 10, 0, 34);
    c.fill();
  },
];

const EYE_PATHS: Pt[][] = [
  spline([[-160, -95], [-40, -122], [80, -118], [170, -100]], 10),
  spline([[-140, 0], [-60, -62], [40, -66], [140, -8]], 10),
  spline([[140, -8], [60, 40], [-60, 44], [-140, 0]], 10),
  line([140, -8], [215, -10]),
  spline([[-24, 40], [-30, 95], [-44, 150]], 8),
  spline([[34, 42], [70, 90], [112, 132], [140, 112], [128, 88], [108, 100]], 8),
];

function drawEgypt(f: Frame) {
  const { ctx, p } = f;
  cover(f, f.tex.sand);

  // tosh bloklari
  art(f, 1 + p * 0.05);
  ctx.strokeStyle = "rgba(90,55,20,0.35)";
  ctx.lineWidth = 3;
  for (let row = -5; row <= 5; row++) {
    const y = row * 95;
    ctx.beginPath();
    ctx.moveTo(-900, y);
    ctx.lineTo(900, y);
    ctx.stroke();
    for (let col = -6; col <= 6; col++) {
      const x = col * 190 + (row % 2 ? 95 : 0);
      ctx.beginPath();
      ctx.moveTo(x, y);
      ctx.lineTo(x, y + 95);
      ctx.stroke();
    }
  }

  // ustunlar va ierogliflar
  const columns = [-470, -340, 340, 470];
  let index = 0;
  columns.forEach((cx, ci) => {
    ctx.strokeStyle = "rgba(120,70,20,0.55)";
    ctx.lineWidth = 3;
    ctx.strokeRect(cx - 52, -320, 104, 620);
    for (let r = 0; r < 5; r++) {
      const delay = 0.04 + ((ci * 5 + r) / 20) * 0.62;
      const k = seg(p, delay, delay + 0.12);
      index++;
      if (k <= 0) continue;
      const glyph = GLYPHS[(r * 3 + ci * 5 + (ci > 1 ? 1 : 0)) % GLYPHS.length];
      const cy = -258 + r * 124;
      const color = r % 3 === 0 ? "#1f4f9e" : r % 3 === 1 ? "#a3402a" : "#2b1a0c";
      ctx.save();
      ctx.translate(cx, cy);
      ctx.scale(1.25 + (1 - easeOut(k)) * 0.4, 1.25 + (1 - easeOut(k)) * 0.4);
      ctx.globalAlpha = easeOut(k);
      ctx.lineWidth = 4.5;
      ctx.lineCap = "round";
      // o'yib yozilgandek soya
      ctx.translate(2, 2.5);
      ctx.strokeStyle = ctx.fillStyle = "rgba(60,30,8,0.5)";
      glyph(ctx);
      ctx.translate(-2, -2.5);
      ctx.strokeStyle = ctx.fillStyle = color;
      glyph(ctx);
      ctx.restore();
    }
  });

  // markazdagi Horus ko'zi
  ctx.save();
  ctx.scale(1.15, 1.15);
  ctx.lineCap = "round";
  ctx.lineJoin = "round";
  const irisK = seg(p, 0.45, 0.65);
  if (irisK > 0) {
    ctx.fillStyle = "#1f4f9e";
    ctx.globalAlpha = irisK;
    ctx.beginPath();
    ctx.arc(0, -14, 30 * easeOut(irisK), 0, Math.PI * 2);
    ctx.fill();
    ctx.globalAlpha = 1;
  }
  ctx.shadowColor = "rgba(255,200,90,0.7)";
  ctx.shadowBlur = 14;
  ctx.strokeStyle = "#d9a237";
  ctx.lineWidth = 15;
  strokeSequence(ctx, EYE_PATHS, seg(p, 0.0, 0.75));
  ctx.restore();

  // oltin nur
  screen(f);
  const g = ctx.createLinearGradient(0, 0, f.w, f.h);
  g.addColorStop(0, "rgba(255,220,150,0.18)");
  g.addColorStop(1, "rgba(80,40,0,0.25)");
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, f.w, f.h);
  vignette(f, 0.6, "40,20,0");
}

// ================= 3. UYG'ONISH DAVRI =================

const VITRUVIAN: Pt[][] = [
  arcPts(0, -10, 280, -Math.PI / 2, Math.PI * 1.5, 90),
  [[-240, -210], [240, -210], [240, 270], [-240, 270], [-240, -210]].flatMap((pt, i, arr) => (i ? line(arr[i - 1] as Pt, pt as Pt, 16).slice(1) : [pt as Pt])),
];
const FIGURE: Pt[][] = [
  arcPts(0, -172, 34, -Math.PI / 2, Math.PI * 1.5, 30),
  line([0, -138], [0, 40]),
  line([-238, -110], [238, -110]),
  line([0, -122], [-252, -205]),
  line([0, -122], [252, -205]),
  line([0, 40], [-42, 268]),
  line([0, 40], [42, 268]),
  line([0, 40], [-172, 228]),
  line([0, 40], [172, 228]),
];

function goldenSpiral(): Pt[] {
  const pts: Pt[] = [];
  const phi = 1.618;
  let size = 300;
  let cx = 120;
  let cy = -40;
  let angle = Math.PI;
  for (let q = 0; q < 7; q++) {
    pts.push(...arcPts(cx, cy, size, angle, angle + Math.PI / 2, 20));
    const next = size / phi;
    const dir = angle + Math.PI / 2;
    cx += Math.cos(dir) * (size - next);
    cy += Math.sin(dir) * (size - next);
    size = next;
    angle += Math.PI / 2;
  }
  return pts;
}
const SPIRAL = goldenSpiral();

function scriptLines(x: number, y: number, rows: number, width: number, seed: number): Pt[][] {
  const rnd = mulberry32(seed);
  return Array.from({ length: rows }, (_, r) =>
    Array.from({ length: 40 }, (_, i) => [x + (i / 39) * width * (0.7 + rnd() * 0.3), y + r * 22 + Math.sin(i * 1.7 + r) * 4 + (rnd() - 0.5) * 3] as Pt),
  );
}
const SCRIPT_LEFT = scriptLines(-520, 140, 6, 200, 3);
const SCRIPT_RIGHT = scriptLines(330, -300, 5, 190, 9);

function drawRenaissance(f: Frame) {
  const { ctx, p } = f;
  cover(f, f.tex.parchment);
  art(f, 1.02 + p * 0.05);
  ctx.lineCap = "round";
  ctx.lineJoin = "round";

  // perspektiva setkasi
  ctx.strokeStyle = "rgba(90,55,25,0.22)";
  ctx.lineWidth = 1.5;
  const gridK = seg(p, 0, 0.5);
  for (let i = -10; i <= 10; i++) strokePartial(ctx, line([0, 120], [i * 90, 420]), gridK);

  // oltin spiral (qizil bo'r)
  ctx.strokeStyle = "rgba(168,70,40,0.7)";
  ctx.lineWidth = 3;
  ctx.setLineDash([2, 7]);
  strokePartial(ctx, SPIRAL, seg(p, 0.35, 0.95));
  ctx.setLineDash([]);

  // aylana va kvadrat
  ctx.strokeStyle = "#4b2e17";
  ctx.lineWidth = 4;
  strokeSequence(ctx, VITRUVIAN, seg(p, 0, 0.45));

  // inson qiyofasi
  ctx.lineWidth = 6;
  const tip = strokeSequence(ctx, FIGURE, seg(p, 0.2, 0.8));

  // qo'lyozma qatorlari
  ctx.strokeStyle = "rgba(75,46,23,0.7)";
  ctx.lineWidth = 2.5;
  strokeSequence(ctx, SCRIPT_LEFT, seg(p, 0.3, 1));
  strokeSequence(ctx, SCRIPT_RIGHT, seg(p, 0.2, 0.95));

  // pero uchi
  if (tip && p < 0.8) {
    ctx.save();
    ctx.translate(tip[0], tip[1]);
    ctx.rotate(-0.7);
    ctx.fillStyle = "#2b1a0c";
    ctx.beginPath();
    ctx.moveTo(0, 0);
    ctx.lineTo(-7, -70);
    ctx.lineTo(7, -70);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }
  vignette(f, 0.55, "70,40,10");
}

// ================= 4. BOSMA VA PLAKAT =================

function drawPrint(f: Frame) {
  const { ctx, p } = f;
  cover(f, f.tex.paper);
  art(f, 1 + p * 0.04);

  // Gutenberg sahifasi: ikki ustun matn
  const textK = seg(p, 0.05, 0.55);
  ctx.fillStyle = "#1b1712";
  const rnd = mulberry32(77);
  const rowsVisible = Math.floor(textK * 22);
  for (let col = 0; col < 2; col++) {
    const x0 = col === 0 ? -400 : 30;
    for (let r = 0; r < 22; r++) {
      const width = r % 7 === 6 ? 160 + rnd() * 120 : 330 + rnd() * 30;
      if (r >= rowsVisible) continue;
      const y = -250 + r * 24;
      const indent = col === 0 && r < 5 ? 110 : 0;
      ctx.globalAlpha = 0.85;
      ctx.fillRect(x0 + indent, y, width - indent, 9);
    }
  }
  ctx.globalAlpha = 1;

  // bosilayotgan katta harflar
  const stamps: [string, number, number, number, string][] = [
    ["P", -345, -175, 0.04, "#b3261e"],
    ["A", 300, 180, 0.17, "#1b1712"],
    ["&", 430, -150, 0.3, "#1b1712"],
  ];
  for (const [ch, x, y, delay, color] of stamps) {
    const k = seg(p, delay, delay + 0.08);
    if (k <= 0) continue;
    const s = 1.6 - easeOut(k) * 0.6;
    ctx.save();
    ctx.translate(x, y);
    ctx.scale(s, s);
    ctx.globalAlpha = easeOut(k);
    ctx.fillStyle = color;
    ctx.font = `700 ${ch === "P" ? 150 : 190}px Georgia, "Times New Roman", serif`;
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(ch, 0, 0);
    ctx.restore();
  }

  // Plakat (konstruktivizm uslubida) — o'ngdan diagonal bo'lib kirib keladi
  const wipe = easeInOut(seg(p, 0.42, 0.72));
  if (wipe > 0) {
    screen(f);
    ctx.save();
    ctx.beginPath();
    const edge = lerp(f.w + f.h, -f.h * 0.5, wipe);
    ctx.moveTo(edge, 0);
    ctx.lineTo(f.w + f.h, 0);
    ctx.lineTo(f.w + f.h, f.h);
    ctx.lineTo(edge - f.h * 0.5, f.h);
    ctx.closePath();
    ctx.clip();
    ctx.fillStyle = "#ece3cf";
    ctx.fillRect(0, 0, f.w, f.h);

    art(f, 1 + p * 0.04);
    const k = (a: number) => easeOut(seg(p, a, a + 0.25));
    ctx.fillStyle = "#d62e1f";
    ctx.beginPath();
    ctx.arc(-120 + (1 - k(0.5)) * -300, -40, 210, 0, Math.PI * 2);
    ctx.fill();

    ctx.save();
    ctx.rotate(-0.42);
    ctx.fillStyle = "#121212";
    ctx.fillRect(-620 + (1 - k(0.55)) * 900, 60, 1240, 70);
    ctx.restore();

    ctx.fillStyle = "#f2c230";
    ctx.beginPath();
    const ty = (1 - k(0.6)) * 500;
    ctx.moveTo(170, 300 + ty);
    ctx.lineTo(420, 300 + ty);
    ctx.lineTo(295, 80 + ty);
    ctx.closePath();
    ctx.fill();

    ctx.fillStyle = "#1d4fa3";
    ctx.fillRect(330, -300 - (1 - k(0.62)) * 400, 120, 120);

    ctx.save();
    ctx.translate(-440, 40);
    ctx.rotate(-Math.PI / 2);
    ctx.fillStyle = "#121212";
    ctx.font = `700 92px ${f.fonts.display}`;
    ctx.textAlign = "center";
    ctx.globalAlpha = k(0.62);
    ctx.fillText("DIZAYN", 0, 0);
    ctx.restore();

    ctx.fillStyle = "#121212";
    ctx.globalAlpha = k(0.66);
    ctx.font = `600 26px ${f.fonts.sans}`;
    ctx.fillText("SHAKL · RANG · G'OYA", -40, 300);
    ctx.globalAlpha = 1;
    ctx.restore();
  }
  vignette(f, 0.35, "60,40,20");
}

// ================= 5. KOMPYUTER GRAFIKASI =================

const PEN_POINTS: Pt[] = [[-230, 60], [-120, -120], [20, -20], [130, -150], [240, 20], [90, 140], [-60, 120], [-230, 60]];
const PEN_PATH = spline(PEN_POINTS, 14, false);

function drawDigital(f: Frame) {
  const { ctx, p, w, h } = f;
  screen(f);
  ctx.fillStyle = "#16171c";
  ctx.fillRect(0, 0, w, h);

  // Interfeys: yuqori panel, asboblar, qatlamlar
  const narrow = w < 720;
  ctx.fillStyle = "#24262d";
  ctx.fillRect(0, 0, w, 38);
  ["#ff5f57", "#febc2e", "#28c840"].forEach((c, i) => {
    ctx.fillStyle = c;
    ctx.beginPath();
    ctx.arc(18 + i * 18, 19, 5.5, 0, Math.PI * 2);
    ctx.fill();
  });
  ctx.fillStyle = "#9aa0ad";
  ctx.font = `600 12px ${f.fonts.sans}`;
  ctx.fillText("Dizayn.psd — 100%", 84, 23);

  ctx.fillStyle = "#1f2127";
  ctx.fillRect(0, 38, 50, h - 38);
  for (let i = 0; i < 9; i++) {
    const active = i === (p < 0.3 ? 4 : 2);
    ctx.fillStyle = active ? "#18a0fb" : "#3a3d46";
    ctx.beginPath();
    ctx.roundRect(13, 56 + i * 38, 24, 24, 6);
    ctx.fill();
  }
  if (!narrow) {
    ctx.fillStyle = "#1f2127";
    ctx.fillRect(w - 220, 38, 220, h - 38);
    ctx.fillStyle = "#9aa0ad";
    ctx.font = `700 11px ${f.fonts.sans}`;
    ctx.fillText("QATLAMLAR", w - 200, 64);
    ["Shakl", "Gradient", "Piksel aylana", "Fon"].forEach((name, i) => {
      const lit = (i === 0 && p > 0.3) || (i === 1 && p > 0.72) || (i === 2 && p < 0.35);
      ctx.fillStyle = lit ? "rgba(24,160,251,0.22)" : "#272a31";
      ctx.fillRect(w - 208, 78 + i * 38, 196, 30);
      ctx.fillStyle = lit ? "#e8f4ff" : "#9aa0ad";
      ctx.font = `600 12px ${f.fonts.sans}`;
      ctx.fillText(name, w - 168, 98 + i * 38);
      ctx.fillStyle = "#4a4e59";
      ctx.fillRect(w - 198, 84 + i * 38, 20, 18);
    });
  }

  art(f, 1 + p * 0.03);
  // Artboard
  ctx.shadowColor = "rgba(0,0,0,0.5)";
  ctx.shadowBlur = 40;
  ctx.fillStyle = "#f4f5f8";
  ctx.fillRect(-340, -240, 680, 480);
  ctx.shadowBlur = 0;

  // Pikseldan vektorga: katta piksellar asta-sekin silliqlashadi
  const pixK = seg(p, 0, 0.4);
  if (pixK < 1) {
    const cell = pixK < 0.33 ? 40 : pixK < 0.66 ? 20 : 10;
    ctx.globalAlpha = 1 - seg(p, 0.3, 0.42);
    for (let x = -200; x < 200; x += cell) {
      for (let y = -200; y < 200; y += cell) {
        const d = Math.hypot(x + cell / 2, y + cell / 2);
        if (d < 170) {
          ctx.fillStyle = `hsl(${200 + (x + y) * 0.15}, 85%, ${55 + (d / 170) * 10}%)`;
          ctx.fillRect(x, y, cell - 1, cell - 1);
        }
      }
    }
    ctx.globalAlpha = 1;
  }

  // Pen tool bilan bezier shakl
  const penK = seg(p, 0.3, 0.85);
  const fillK = seg(p, 0.75, 0.95);
  if (fillK > 0) {
    const g = ctx.createLinearGradient(-230, -150, 240, 140);
    g.addColorStop(0, f.colors.brand);
    g.addColorStop(0.5, f.colors.brand2);
    g.addColorStop(1, f.colors.brand3);
    ctx.globalAlpha = fillK;
    ctx.fillStyle = g;
    ctx.beginPath();
    PEN_PATH.forEach(([x, y], i) => (i ? ctx.lineTo(x, y) : ctx.moveTo(x, y)));
    ctx.fill();
    ctx.globalAlpha = 1;
  }
  ctx.strokeStyle = "#18a0fb";
  ctx.lineWidth = 3;
  const tip = strokePartial(ctx, PEN_PATH, penK);
  const placed = Math.floor(penK * (PEN_POINTS.length - 1)) + (penK > 0 ? 1 : 0);
  PEN_POINTS.slice(0, placed).forEach(([x, y], i) => {
    const prev = PEN_POINTS[Math.max(0, i - 1)];
    const next = PEN_POINTS[Math.min(PEN_POINTS.length - 1, i + 1)];
    const dx = (next[0] - prev[0]) * 0.22;
    const dy = (next[1] - prev[1]) * 0.22;
    ctx.strokeStyle = "rgba(24,160,251,0.7)";
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(x - dx, y - dy);
    ctx.lineTo(x + dx, y + dy);
    ctx.stroke();
    for (const [hx, hy] of [[x - dx, y - dy], [x + dx, y + dy]]) {
      ctx.fillStyle = "#18a0fb";
      ctx.beginPath();
      ctx.arc(hx, hy, 4, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.fillStyle = "#fff";
    ctx.strokeStyle = "#18a0fb";
    ctx.lineWidth = 2;
    ctx.fillRect(x - 6, y - 6, 12, 12);
    ctx.strokeRect(x - 6, y - 6, 12, 12);
  });

  // Tanlash ramkasi
  if (p > 0.88) {
    ctx.globalAlpha = seg(p, 0.88, 0.95);
    ctx.strokeStyle = "#18a0fb";
    ctx.lineWidth = 1.5;
    ctx.strokeRect(-245, -165, 500, 320);
    for (const [x, y] of [[-245, -165], [255, -165], [-245, 155], [255, 155], [5, -165], [5, 155]]) {
      ctx.fillStyle = "#fff";
      ctx.fillRect(x - 5, y - 5, 10, 10);
      ctx.strokeRect(x - 5, y - 5, 10, 10);
    }
    ctx.globalAlpha = 1;
  }

  // Sichqoncha kursori
  const cursor: Pt = tip ?? [lerp(260, -230, seg(p, 0, 0.3)), lerp(200, 60, seg(p, 0, 0.3))];
  ctx.save();
  ctx.translate(cursor[0] + 4, cursor[1] + 4);
  ctx.scale(1.6, 1.6);
  ctx.fillStyle = "#111";
  ctx.strokeStyle = "#fff";
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  ctx.moveTo(0, 0);
  ctx.lineTo(0, 18);
  ctx.lineTo(4.5, 14);
  ctx.lineTo(8, 21);
  ctx.lineTo(10.5, 20);
  ctx.lineTo(7, 13);
  ctx.lineTo(13, 13);
  ctx.closePath();
  ctx.fill();
  ctx.stroke();
  ctx.restore();

  // Oxirida RGB "glitch"
  const glitch = seg(p, 0.9, 1);
  if (glitch > 0) {
    screen(f);
    const rnd = mulberry32(Math.floor(f.t * 30));
    for (let i = 0; i < 7; i++) {
      ctx.fillStyle = ["rgba(255,0,80,0.18)", "rgba(0,200,255,0.18)", "rgba(120,255,120,0.12)"][i % 3];
      ctx.fillRect(0, rnd() * h, w, 4 + rnd() * 24 * glitch);
    }
  }
}

// ================= 6. SUN'IY INTELLEKT =================

type Particle = { sx: number; sy: number; tx: number; ty: number; n1: number; n2: number; hue: number; phase: number };
let particleCache: { key: string; list: Particle[] } | null = null;

/** Brend nomi ekranga sig'adigan maksimal kenglik (dizayn birligida) */
function textMaxWidth(f: Frame) {
  return Math.min(1040, (f.w / f.S) * 0.88);
}

function particles(f: Frame): Particle[] {
  const maxW = textMaxWidth(f);
  const key = `${f.brandName}|${f.fonts.display}|${f.w < 700}|${Math.round(maxW)}`;
  if (particleCache?.key === key) return particleCache.list;

  const text = f.brandName.toUpperCase();
  const off = document.createElement("canvas");
  off.width = 1100;
  off.height = 260;
  const octx = off.getContext("2d")!;
  octx.fillStyle = "#fff";
  octx.textAlign = "center";
  octx.textBaseline = "middle";
  let size = 170;
  octx.font = `700 ${size}px ${f.fonts.display}`;
  while (octx.measureText(text).width > maxW && size > 40) {
    size -= 6;
    octx.font = `700 ${size}px ${f.fonts.display}`;
  }
  octx.fillText(text, 550, 130);
  const data = octx.getImageData(0, 0, off.width, off.height).data;
  const step = f.w < 700 ? 9 : 6;
  const targets: Pt[] = [];
  for (let y = 0; y < off.height; y += step) {
    for (let x = 0; x < off.width; x += step) {
      if (data[(y * off.width + x) * 4 + 3] > 128) targets.push([x - 550, y - 130 - 20]);
    }
  }
  const rnd = mulberry32(99);
  const list: Particle[] = targets.map(([tx, ty]) => ({
    sx: (rnd() - 0.5) * 1300,
    sy: (rnd() - 0.5) * 820,
    tx,
    ty,
    n1: Math.floor(rnd() * targets.length),
    n2: Math.floor(rnd() * targets.length),
    hue: (tx + 550) / 1100,
    phase: rnd() * Math.PI * 2,
  }));
  // Yaqin qo'shnilarni tanlash (neyron tarmoq chiziqlari uchun)
  list.forEach((pt, i) => {
    let best = -1;
    let second = -1;
    let bd = Infinity;
    let sd = Infinity;
    for (let k = 0; k < 40; k++) {
      const j = Math.floor(rnd() * list.length);
      if (j === i) continue;
      const d = Math.hypot(list[j].sx - pt.sx, list[j].sy - pt.sy);
      if (d < bd) {
        sd = bd;
        second = best;
        bd = d;
        best = j;
      } else if (d < sd) {
        sd = d;
        second = j;
      }
    }
    pt.n1 = best;
    pt.n2 = second;
  });
  particleCache = { key, list };
  return list;
}

function mixColor(a: string, b: string, t: number): string {
  if (!/^#[0-9a-f]{6}$/i.test(a) || !/^#[0-9a-f]{6}$/i.test(b)) return t < 0.5 ? a : b;
  const pa = parseInt(a.slice(1), 16);
  const pb = parseInt(b.slice(1), 16);
  const r = Math.round(lerp((pa >> 16) & 255, (pb >> 16) & 255, t));
  const g = Math.round(lerp((pa >> 8) & 255, (pb >> 8) & 255, t));
  const bl = Math.round(lerp(pa & 255, pb & 255, t));
  return `rgb(${r},${g},${bl})`;
}

function drawAI(f: Frame) {
  const { ctx, p, t, w, h } = f;
  screen(f);
  ctx.fillStyle = "#050b1c";
  ctx.fillRect(0, 0, w, h);
  const glow = (x: number, y: number, r: number, color: string, a: number) => {
    const g = ctx.createRadialGradient(x, y, 0, x, y, r);
    g.addColorStop(0, color);
    g.addColorStop(1, "rgba(5,11,28,0)");
    ctx.globalAlpha = a;
    ctx.fillStyle = g;
    ctx.fillRect(0, 0, w, h);
    ctx.globalAlpha = 1;
  };
  glow(w * 0.3 + Math.sin(t) * 40, h * 0.4, Math.max(w, h) * 0.5, f.colors.brand, 0.35);
  glow(w * 0.72, h * 0.6 + Math.cos(t) * 30, Math.max(w, h) * 0.45, f.colors.brand2, 0.35);
  glow(w * 0.55, h * 0.2, Math.max(w, h) * 0.3, f.colors.brand3, 0.18 * seg(p, 0.4, 1));

  art(f, 1);
  const list = particles(f);
  const form = easeInOut(seg(p, 0.12, 0.55));

  // Neyron tarmoq chiziqlari (shakllanguncha)
  const linesA = (1 - form) * 0.32 * seg(p, 0, 0.1);
  const pos = (pt: Particle): Pt => {
    const driftX = Math.sin(t * 1.3 + pt.phase) * 18 * (1 - form);
    const driftY = Math.cos(t * 1.1 + pt.phase) * 18 * (1 - form);
    return [lerp(pt.sx + driftX, pt.tx, form), lerp(pt.sy + driftY, pt.ty, form)];
  };
  const positions = list.map(pos);
  if (linesA > 0.01) {
    ctx.lineWidth = 1;
    ctx.strokeStyle = f.colors.brand;
    ctx.globalAlpha = linesA;
    ctx.beginPath();
    for (let i = 0; i < list.length; i += 3) {
      const a = positions[i];
      for (const j of [list[i].n1, list[i].n2]) {
        if (j < 0) continue;
        const b = positions[j];
        if (Math.hypot(a[0] - b[0], a[1] - b[1]) < 110) {
          ctx.moveTo(a[0], a[1]);
          ctx.lineTo(b[0], b[1]);
        }
      }
    }
    ctx.stroke();
    ctx.globalAlpha = 1;
  }

  // Zarrachalar
  ctx.globalCompositeOperation = "lighter";
  const size = f.w < 700 ? 3.4 : 2.6;
  list.forEach((pt, i) => {
    const [x, y] = positions[i];
    ctx.fillStyle = pt.hue < 0.5 ? mixColor(f.colors.brand, f.colors.brand2, pt.hue * 2) : mixColor(f.colors.brand2, f.colors.brand3, (pt.hue - 0.5) * 2);
    ctx.globalAlpha = 0.55 + 0.45 * Math.sin(t * 4 + pt.phase) * (1 - form) + 0.4 * form;
    ctx.fillRect(x - size / 2, y - size / 2, size, size);
  });
  ctx.globalAlpha = 1;
  ctx.globalCompositeOperation = "source-over";

  // Aniq matn va nur yalt etishi
  const solid = seg(p, 0.5, 0.75);
  if (solid > 0) {
    const text = f.brandName.toUpperCase();
    let fs = 170;
    ctx.font = `700 ${fs}px ${f.fonts.display}`;
    while (ctx.measureText(text).width > textMaxWidth(f) && fs > 40) {
      fs -= 6;
      ctx.font = `700 ${fs}px ${f.fonts.display}`;
    }
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    const g = ctx.createLinearGradient(-520, 0, 520, 0);
    g.addColorStop(0, f.colors.brand);
    g.addColorStop(0.5, f.colors.brand2);
    g.addColorStop(1, f.colors.brand3);
    ctx.shadowColor = f.colors.brand2;
    ctx.shadowBlur = 40 * solid;
    ctx.globalAlpha = solid;
    ctx.fillStyle = g;
    ctx.fillText(text, 0, -20);
    ctx.shadowBlur = 0;

    const sweep = seg(p, 0.62, 0.9);
    if (sweep > 0 && sweep < 1) {
      ctx.save();
      ctx.globalCompositeOperation = "lighter";
      const sx = lerp(-700, 700, sweep);
      const sg = ctx.createLinearGradient(sx - 120, 0, sx + 120, 0);
      sg.addColorStop(0, "rgba(255,255,255,0)");
      sg.addColorStop(0.5, "rgba(255,255,255,0.55)");
      sg.addColorStop(1, "rgba(255,255,255,0)");
      ctx.fillStyle = sg;
      ctx.fillText(text, 0, -20);
      ctx.restore();
    }
    ctx.globalAlpha = 1;
  }

  // "Prompt" yozilmoqda
  const prompt = "› yarat: kelajak dizayni ✦";
  const chars = Math.floor(seg(p, 0.05, 0.45) * prompt.length);
  ctx.font = `500 28px ui-monospace, SFMono-Regular, Menlo, monospace`;
  ctx.textAlign = "center";
  ctx.fillStyle = f.colors.brand;
  ctx.globalAlpha = 0.9 * (1 - seg(p, 0.85, 1));
  ctx.fillText(prompt.slice(0, chars) + (Math.floor(t * 3) % 2 ? "▍" : " "), 0, -230);
  ctx.globalAlpha = 1;

  // Sayt foniga silliq o'tish
  const out = seg(p, 0.88, 1);
  if (out > 0) {
    screen(f);
    ctx.fillStyle = `rgba(11,21,48,${out})`;
    ctx.fillRect(0, 0, w, h);
  }
}

const DRAWERS = [drawCave, drawEgypt, drawRenaissance, drawPrint, drawDigital, drawAI];

/** Vaqt `t` dagi kadrni chizadi. Joriy sahna indeksini qaytaradi. */
export function renderFrame(base: Omit<Frame, "p">): number {
  const { ctx, t } = base;
  let index = SCENES.length - 1;
  for (let i = 0; i < SCENES.length; i++) {
    if (t < sceneStart(i) + SCENES[i].duration) {
      index = i;
      break;
    }
  }
  const start = sceneStart(index);
  const p = clamp((t - start) / SCENES[index].duration);
  ctx.save();
  ctx.globalAlpha = 1;
  DRAWERS[index]({ ...base, p });
  ctx.restore();

  // Keyingi sahnaga yumshoq o'tish
  const toEnd = start + SCENES[index].duration - t;
  if (index < SCENES.length - 1 && toEnd < FADE) {
    const k = 1 - toEnd / FADE;
    // Keyingi sahna alohida qatlamda chiziladi va ustiga shaffoflik bilan qo'yiladi
    const layer = getLayer(ctx.canvas);
    const lctx = layer.getContext("2d")!;
    lctx.setTransform(1, 0, 0, 1, 0, 0);
    lctx.clearRect(0, 0, layer.width, layer.height);
    lctx.save();
    DRAWERS[index + 1]({ ...base, ctx: lctx, p: 0 });
    lctx.restore();
    ctx.save();
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.globalAlpha = easeInOut(k);
    ctx.drawImage(layer, 0, 0);
    ctx.restore();
  }
  return index;
}

let layerCanvas: HTMLCanvasElement | null = null;
function getLayer(main: HTMLCanvasElement): HTMLCanvasElement {
  if (!layerCanvas) layerCanvas = document.createElement("canvas");
  if (layerCanvas.width !== main.width || layerCanvas.height !== main.height) {
    layerCanvas.width = main.width;
    layerCanvas.height = main.height;
  }
  return layerCanvas;
}
