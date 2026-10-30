"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { LogoMark } from "@/components/Logo";
import { IntroAudio } from "./audio";
import { SCENES, TOTAL_DURATION, createTextures, renderFrame, type Textures } from "./scenes";

const SEEN_KEY = "pp_intro_seen";
export const REPLAY_EVENT = "pp:replay-intro";

type Phase = "hidden" | "gate" | "playing" | "closing";

function readCssVar(name: string, fallback: string) {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
}

/**
 * Sayt ochilganda chiqadigan ~10 soniyalik intro: g'or rasmlaridan AI dizayngacha.
 * Brauzerlar ovozni avtomatik o'ynatishni bloklagani uchun avval "Boshlash" tugmasi ko'rsatiladi.
 * Admin panelda .mp4 video yuklansa, Canvas o'rniga o'sha video o'ynaydi.
 */
export function Intro({ brandName, videoUrl, enabled }: { brandName: string; videoUrl: string | null; enabled: boolean }) {
  const [phase, setPhase] = useState<Phase>("hidden");
  const [scene, setScene] = useState(0);
  const [muted, setMuted] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const progressRef = useRef<HTMLDivElement>(null);
  const audioRef = useRef<IntroAudio | null>(null);
  const rafRef = useRef(0);
  const texRef = useRef<Textures | null>(null);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    let seen = false;
    try {
      seen = sessionStorage.getItem(SEEN_KEY) === "1";
    } catch {}
    if (enabled && (!seen || params.has("intro"))) setPhase("gate");
    const replay = () => setPhase("gate");
    window.addEventListener(REPLAY_EVENT, replay);
    return () => window.removeEventListener(REPLAY_EVENT, replay);
  }, [enabled]);

  // Intro ochiq paytda sahifa aylantirilmasin
  useEffect(() => {
    if (phase === "hidden") return;
    const prev = document.documentElement.style.overflow;
    document.documentElement.style.overflow = "hidden";
    return () => {
      document.documentElement.style.overflow = prev;
    };
  }, [phase]);

  const finish = useCallback(() => {
    cancelAnimationFrame(rafRef.current);
    audioRef.current?.close();
    audioRef.current = null;
    videoRef.current?.pause();
    try {
      sessionStorage.setItem(SEEN_KEY, "1");
    } catch {}
    setPhase("closing");
    window.setTimeout(() => setPhase("hidden"), 700);
  }, []);

  const start = useCallback(
    (withSound: boolean) => {
      setMuted(!withSound);
      setScene(0);
      setPhase("playing");
      // Safari ovozli videoni faqat bosish ichida play() chaqirilsa ruxsat beradi
      if (videoUrl && videoRef.current) {
        videoRef.current.muted = !withSound;
        videoRef.current.play().catch(() => {
          if (videoRef.current) {
            videoRef.current.muted = true;
            setMuted(true);
            videoRef.current.play().catch(finish);
          }
        });
      }
      if (!videoUrl && withSound) {
        try {
          const audio = new IntroAudio();
          audio.ctx.resume();
          audio.play();
          audioRef.current = audio;
        } catch {
          audioRef.current = null;
        }
      }
    },
    [videoUrl, finish],
  );

  // Canvas animatsiya sikli
  useEffect(() => {
    if (phase !== "playing" || videoUrl) return;
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const small = window.innerWidth < 700;
    texRef.current ??= createTextures(small);
    const colors = {
      brand: readCssVar("--color-brand", "#38c8ff"),
      brand2: readCssVar("--color-brand-2", "#7b61ff"),
      brand3: readCssVar("--color-brand-3", "#ff5ccd"),
    };
    const fonts = {
      display: readCssVar("--font-unbounded", "sans-serif"),
      sans: readCssVar("--font-manrope", "sans-serif"),
    };

    let w = 0;
    let h = 0;
    let dpr = 1;
    const resize = () => {
      dpr = Math.min(window.devicePixelRatio || 1, 2);
      w = window.innerWidth;
      h = window.innerHeight;
      canvas.width = Math.round(w * dpr);
      canvas.height = Math.round(h * dpr);
    };
    resize();
    window.addEventListener("resize", resize);

    // Faqat ishlab chiqishda: ?introAt=3.5 — kadrni to'xtatib ko'rish
    const debugAt = process.env.NODE_ENV !== "production" ? Number(new URLSearchParams(location.search).get("introAt")) : NaN;
    const startedAt = performance.now();
    let lastScene = -1;
    const tick = () => {
      const t = Number.isFinite(debugAt) && debugAt > 0 ? debugAt : (performance.now() - startedAt) / 1000;
      // Telefonda (vertikal ekran) rasm kattaroq ko'rinsin — chetlari biroz kesilsa ham
      const S = Math.min(w / (w < h ? 760 : 1150), h / 800);
      const index = renderFrame({ ctx, w, h, dpr, S, t: Math.min(t, TOTAL_DURATION), tex: texRef.current!, colors, fonts, brandName });
      if (index !== lastScene) {
        lastScene = index;
        setScene(index);
      }
      if (progressRef.current) progressRef.current.style.transform = `scaleX(${Math.min(1, t / TOTAL_DURATION)})`;
      if (t >= TOTAL_DURATION && !(debugAt > 0)) {
        finish();
        return;
      }
      rafRef.current = requestAnimationFrame(tick);
    };
    rafRef.current = requestAnimationFrame(tick);
    return () => {
      cancelAnimationFrame(rafRef.current);
      window.removeEventListener("resize", resize);
    };
  }, [phase, videoUrl, brandName, finish]);

  useEffect(() => {
    audioRef.current?.setMuted(muted);
    if (videoRef.current) videoRef.current.muted = muted;
  }, [muted]);

  // Esc — o'tkazib yuborish
  useEffect(() => {
    if (phase === "hidden") return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && finish();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [phase, finish]);

  if (phase === "hidden") return null;
  const info = SCENES[scene];

  return (
    <div
      className={`fixed inset-0 z-[100] bg-ink-950 transition-opacity duration-700 ${phase === "closing" ? "pointer-events-none opacity-0" : "opacity-100"}`}
      role="dialog"
      aria-label="Dizayn tarixi — intro"
    >
      {phase === "gate" && (
        <div className="relative flex h-full flex-col items-center justify-center overflow-hidden px-6 text-center">
          <div className="bg-grid absolute inset-0 opacity-60" />
          <div className="glow -top-20 left-1/4 h-80 w-80 bg-brand" />
          <div className="glow right-1/4 bottom-0 h-96 w-96 bg-brand-2" />
          <LogoMark className="relative mb-8 h-20 w-20 animate-float" />
          <p className="section-eyebrow relative">10 soniyalik sayohat</p>
          <h1 className="relative max-w-3xl font-display text-3xl leading-tight font-semibold sm:text-5xl">
            Dizayn tarixi: <span className="text-gradient">g&apos;or devoridan sun&apos;iy intellektgacha</span>
          </h1>
          <p className="relative mt-4 max-w-xl text-mist/80">
            Ibtidoiy odamlar chizgan ilk rasmlardan bugungi AI dizayngacha bo&apos;lgan yo&apos;lni ko&apos;ring.
          </p>
          <div className="relative mt-10 flex flex-col items-center gap-3 sm:flex-row">
            <button className="btn-primary px-8 py-4 text-base" onClick={() => start(true)} autoFocus>
              <svg viewBox="0 0 24 24" className="h-5 w-5" fill="currentColor" aria-hidden>
                <path d="M8 5.14v13.72a1 1 0 0 0 1.52.85l10.6-6.86a1 1 0 0 0 0-1.7L9.52 4.29A1 1 0 0 0 8 5.14Z" />
              </svg>
              Boshlash (ovoz bilan)
            </button>
            <button className="btn-ghost px-6 py-4" onClick={() => start(false)}>
              Ovozsiz ko&apos;rish
            </button>
          </div>
          <button className="relative mt-8 text-sm text-muted underline-offset-4 hover:text-white hover:underline" onClick={finish}>
            O&apos;tkazib yuborish →
          </button>
        </div>
      )}

      {videoUrl && (
        <video
          ref={videoRef}
          src={videoUrl}
          className={`absolute inset-0 h-full w-full object-cover ${phase === "gate" ? "invisible" : ""}`}
          preload="auto"
          playsInline
          onEnded={finish}
          onError={() => phase === "playing" && finish()}
        />
      )}

      {(phase === "playing" || phase === "closing") && (
        <>
          {!videoUrl && <canvas ref={canvasRef} className="block h-full w-full" />}

          {!videoUrl && (
            <div className="pointer-events-none absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/85 via-black/45 to-transparent px-5 pt-24 pb-10 sm:px-12 sm:pb-14">
              <div key={info.key} className="animate-[intro-caption_0.6s_ease-out]">
                <p className="font-display text-xs font-medium tracking-[0.22em] text-brand uppercase sm:text-sm">{info.era}</p>
                <p className="mt-2 font-display text-2xl font-semibold text-white drop-shadow-lg sm:text-4xl">{info.title}</p>
              </div>
              <div className="mt-5 flex gap-1.5">
                {SCENES.map((s, i) => (
                  <span key={s.key} className={`h-1 rounded-full transition-all duration-500 ${i === scene ? "w-8 bg-brand" : i < scene ? "w-3 bg-white/70" : "w-3 bg-white/25"}`} />
                ))}
              </div>
            </div>
          )}

          <div className="absolute inset-x-0 top-0 h-1 bg-white/10">
            <div ref={progressRef} className="h-full origin-left scale-x-0 bg-gradient-to-r from-brand via-brand-2 to-brand-3" />
          </div>

          <div className="absolute top-4 right-4 flex gap-2 sm:top-6 sm:right-6">
            <button
              className="btn-ghost h-10 w-10 !p-0 backdrop-blur"
              onClick={() => setMuted((m) => !m)}
              aria-label={muted ? "Ovozni yoqish" : "Ovozni o'chirish"}
              title={muted ? "Ovozni yoqish" : "Ovozni o'chirish"}
            >
              {muted ? (
                <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
                  <path d="M11 5 6 9H2v6h4l5 4V5Z" />
                  <path d="m23 9-6 6M17 9l6 6" />
                </svg>
              ) : (
                <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
                  <path d="M11 5 6 9H2v6h4l5 4V5Z" />
                  <path d="M15.5 8.5a5 5 0 0 1 0 7M19 5a10 10 0 0 1 0 14" />
                </svg>
              )}
            </button>
            <button className="btn-ghost backdrop-blur" onClick={finish}>
              O&apos;tkazib yuborish
            </button>
          </div>
        </>
      )}
    </div>
  );
}
