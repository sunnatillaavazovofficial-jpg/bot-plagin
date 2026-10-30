"use client";

import { useEffect, useRef } from "react";

/**
 * AI robot: boshi va ko'zlari sichqoncha (telefonda — barmoq) qayerda bo'lsa, o'sha tomonga qaraydi.
 * Harakatsiz qolsa, o'zi sekin atrofga qarab turadi va vaqti-vaqti bilan ko'z qisadi.
 * `speaking` — matn yozilayotganda og'iz chiroqlari "gapiradi".
 */
export function Robot({ speaking }: { speaking: boolean }) {
  const rootRef = useRef<SVGSVGElement>(null);
  const headRef = useRef<SVGGElement>(null);
  const eyesRef = useRef<SVGGElement>(null);
  const bodyRef = useRef<SVGGElement>(null);
  const lidsRef = useRef<SVGGElement>(null);

  useEffect(() => {
    const target = { x: 0, y: 0 };
    const current = { x: 0, y: 0 };
    let lastInput = 0;
    let raf = 0;
    let nextBlink = performance.now() + 2500;
    let blinkUntil = 0;
    const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    const aim = (clientX: number, clientY: number) => {
      const rect = rootRef.current?.getBoundingClientRect();
      if (!rect) return;
      const cx = rect.left + rect.width / 2;
      const cy = rect.top + rect.height * 0.33;
      target.x = Math.max(-1, Math.min(1, (clientX - cx) / (window.innerWidth * 0.45)));
      target.y = Math.max(-1, Math.min(1, (clientY - cy) / (window.innerHeight * 0.45)));
      lastInput = performance.now();
    };
    const onPointer = (e: PointerEvent) => aim(e.clientX, e.clientY);
    const onTouch = (e: TouchEvent) => e.touches[0] && aim(e.touches[0].clientX, e.touches[0].clientY);

    window.addEventListener("pointermove", onPointer, { passive: true });
    window.addEventListener("pointerdown", onPointer, { passive: true });
    window.addEventListener("touchstart", onTouch, { passive: true });

    const loop = (now: number) => {
      // Harakatsiz bo'lsa — o'zi atrofga qaraydi
      if (now - lastInput > 2500) {
        const s = now / 1000;
        target.x = Math.sin(s * 0.7) * 0.55 + Math.sin(s * 1.9) * 0.15;
        target.y = Math.cos(s * 0.5) * 0.3;
      }
      const ease = reduced ? 1 : 0.09;
      current.x += (target.x - current.x) * ease;
      current.y += (target.y - current.y) * ease;
      const { x, y } = current;

      headRef.current?.setAttribute("transform", `translate(${x * 16} ${y * 10}) rotate(${x * 9} 200 160)`);
      eyesRef.current?.setAttribute("transform", `translate(${x * 24} ${y * 18})`);
      bodyRef.current?.setAttribute("transform", `translate(${x * 5} 0) rotate(${x * 2.5} 200 330)`);

      if (now > nextBlink) {
        blinkUntil = now + 150;
        nextBlink = now + 2200 + Math.random() * 3200;
      }
      lidsRef.current?.setAttribute("transform", `translate(0 155) scale(1 ${now < blinkUntil ? 0.08 : 1}) translate(0 -155)`);
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);

    return () => {
      cancelAnimationFrame(raf);
      window.removeEventListener("pointermove", onPointer);
      window.removeEventListener("pointerdown", onPointer);
      window.removeEventListener("touchstart", onTouch);
    };
  }, []);

  return (
    <svg ref={rootRef} viewBox="0 0 400 470" className="h-full w-full overflow-visible" role="img" aria-label="PP Design AI robot">
      <defs>
        <linearGradient id="rb-shell" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor="#2a3f78" />
          <stop offset="0.55" stopColor="#16244f" />
          <stop offset="1" stopColor="#0c1636" />
        </linearGradient>
        <linearGradient id="rb-edge" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" style={{ stopColor: "var(--color-brand)" }} />
          <stop offset="0.6" style={{ stopColor: "var(--color-brand-2)" }} />
          <stop offset="1" style={{ stopColor: "var(--color-brand-3)" }} />
        </linearGradient>
        <radialGradient id="rb-eye" cx="0.4" cy="0.35" r="0.7">
          <stop offset="0" stopColor="#ffffff" />
          <stop offset="0.35" style={{ stopColor: "var(--color-brand)" }} />
          <stop offset="1" style={{ stopColor: "var(--color-brand-2)" }} />
        </radialGradient>
        <radialGradient id="rb-halo" cx="0.5" cy="0.5" r="0.5">
          <stop offset="0" style={{ stopColor: "var(--color-brand-2)", stopOpacity: 0.45 }} />
          <stop offset="1" style={{ stopColor: "var(--color-brand-2)", stopOpacity: 0 }} />
        </radialGradient>
        <filter id="rb-glow" x="-50%" y="-50%" width="200%" height="200%">
          <feGaussianBlur stdDeviation="6" result="blur" />
          <feMerge>
            <feMergeNode in="blur" />
            <feMergeNode in="SourceGraphic" />
          </feMerge>
        </filter>
        <filter id="rb-soft" x="-50%" y="-50%" width="200%" height="200%">
          <feGaussianBlur stdDeviation="12" />
        </filter>
      </defs>

      {/* Orqadagi nur halqasi */}
      <circle cx="200" cy="215" r="200" fill="url(#rb-halo)" />
      <circle cx="200" cy="215" r="170" fill="none" stroke="url(#rb-edge)" strokeOpacity="0.25" strokeDasharray="2 10" strokeLinecap="round">
        <animateTransform attributeName="transform" type="rotate" from="0 200 215" to="360 200 215" dur="40s" repeatCount="indefinite" />
      </circle>

      {/* Soyasi */}
      <ellipse cx="200" cy="452" rx="105" ry="12" fill="#000" opacity="0.45" filter="url(#rb-soft)" />

      <g className="animate-float" style={{ transformOrigin: "200px 260px" }}>
        {/* Tana */}
        <g ref={bodyRef}>
          {/* Chap qo'l */}
          <rect x="62" y="282" width="38" height="112" rx="19" fill="url(#rb-shell)" stroke="url(#rb-edge)" strokeOpacity="0.5" transform="rotate(14 81 290)" />
          <circle cx="66" cy="392" r="16" fill="#1b2b5c" stroke="url(#rb-edge)" strokeOpacity="0.6" />
          {/* O'ng qo'l — qalam ushlagan */}
          <g transform="rotate(-38 320 290)">
            <rect x="300" y="282" width="38" height="104" rx="19" fill="url(#rb-shell)" stroke="url(#rb-edge)" strokeOpacity="0.5" />
            <circle cx="319" cy="386" r="16" fill="#1b2b5c" stroke="url(#rb-edge)" strokeOpacity="0.6" />
            <g transform="translate(312 360) rotate(28)">
              <rect x="0" y="0" width="10" height="70" rx="3" style={{ fill: "var(--color-brand-3)" }} />
              <path d="M0 70 L5 86 L10 70 Z" fill="#f6e7d0" />
              <rect x="0" y="-4" width="10" height="8" rx="2" fill="#e6ecff" />
            </g>
          </g>

          <rect x="110" y="262" width="180" height="152" rx="54" fill="url(#rb-shell)" stroke="url(#rb-edge)" strokeOpacity="0.55" strokeWidth="2" />
          <path d="M132 300 Q200 280 268 300" stroke="#ffffff" strokeOpacity="0.12" strokeWidth="6" fill="none" strokeLinecap="round" />
          {/* Ko'krakdagi belgi */}
          <circle cx="200" cy="340" r="30" fill="#060d22" stroke="url(#rb-edge)" strokeWidth="2" />
          <g filter="url(#rb-glow)">
            <path d="M188 354v-26h9a7 7 0 0 1 0 14h-9" fill="none" style={{ stroke: "var(--color-brand)" }} strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
            <path d="M200 357v-19h7a6 6 0 0 1 0 12h-7" fill="none" style={{ stroke: "var(--color-brand-3)" }} strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
          </g>
          <circle cx="146" cy="386" r="5" style={{ fill: "var(--color-brand)" }}>
            <animate attributeName="opacity" values="1;0.3;1" dur="1.6s" repeatCount="indefinite" />
          </circle>
          <circle cx="164" cy="390" r="4" style={{ fill: "var(--color-brand-3)" }}>
            <animate attributeName="opacity" values="0.3;1;0.3" dur="1.6s" repeatCount="indefinite" />
          </circle>
          {/* Bo'yin */}
          <rect x="178" y="236" width="44" height="34" rx="10" fill="#0e1a3d" stroke="url(#rb-edge)" strokeOpacity="0.4" />
        </g>

        {/* Bosh */}
        <g ref={headRef}>
          {/* Antenna */}
          <line x1="200" y1="76" x2="200" y2="36" stroke="#5b6fa8" strokeWidth="5" strokeLinecap="round" />
          <circle cx="200" cy="28" r="11" style={{ fill: "var(--color-brand-3)" }} filter="url(#rb-glow)">
            <animate attributeName="r" values="10;12.5;10" dur="1.8s" repeatCount="indefinite" />
          </circle>

          {/* Quloqlar */}
          <rect x="66" y="128" width="30" height="64" rx="15" fill="#13214a" stroke="url(#rb-edge)" strokeOpacity="0.6" />
          <rect x="304" y="128" width="30" height="64" rx="15" fill="#13214a" stroke="url(#rb-edge)" strokeOpacity="0.6" />

          <rect x="86" y="72" width="228" height="176" rx="66" fill="url(#rb-shell)" stroke="url(#rb-edge)" strokeWidth="2.5" />
          <path d="M120 96 Q200 74 280 96" stroke="#ffffff" strokeOpacity="0.16" strokeWidth="7" fill="none" strokeLinecap="round" />

          {/* Vizor */}
          <rect x="112" y="104" width="176" height="110" rx="50" fill="#040918" stroke="url(#rb-edge)" strokeOpacity="0.8" strokeWidth="2" />
          <rect x="120" y="110" width="160" height="40" rx="20" fill="#ffffff" opacity="0.04" />

          {/* Ko'zlar */}
          <g ref={eyesRef}>
            <g ref={lidsRef}>
              <g filter="url(#rb-glow)">
                <ellipse cx="164" cy="155" rx="21" ry="25" fill="url(#rb-eye)" />
                <ellipse cx="236" cy="155" rx="21" ry="25" fill="url(#rb-eye)" />
              </g>
              <circle cx="157" cy="145" r="6" fill="#fff" opacity="0.9" />
              <circle cx="229" cy="145" r="6" fill="#fff" opacity="0.9" />
            </g>
          </g>

          {/* Og'iz — gapirayotganda tebranadi */}
          <g transform="translate(200 194)">
            {[-18, -9, 0, 9, 18].map((x, i) => (
              <rect
                key={x}
                x={x - 2.5}
                y={-3}
                width="5"
                height="6"
                rx="2.5"
                style={{
                  fill: "var(--color-brand)",
                  transformBox: "fill-box",
                  transformOrigin: "center",
                  animation: speaking ? `rb-talk 0.${3 + (i % 3)}s ease-in-out ${i * 0.07}s infinite alternate` : "none",
                }}
              />
            ))}
          </g>

          {/* Yonoq chiroqlari */}
          <ellipse cx="130" cy="192" rx="10" ry="5" style={{ fill: "var(--color-brand-3)" }} opacity="0.35" />
          <ellipse cx="270" cy="192" rx="10" ry="5" style={{ fill: "var(--color-brand-3)" }} opacity="0.35" />
        </g>
      </g>
    </svg>
  );
}
