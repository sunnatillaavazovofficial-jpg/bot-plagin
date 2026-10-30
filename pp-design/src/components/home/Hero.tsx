"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { Robot } from "./Robot";

/** Bosh sahifa: robot + yozilib turadigan asosiy gaplar */
export function Hero({ badge, lines, subtitle }: { badge: string; lines: string[]; subtitle: string }) {
  const items = lines.length ? lines : ["Dizayn kelajagi shu yerda."];
  const [text, setText] = useState("");
  const [speaking, setSpeaking] = useState(false);

  useEffect(() => {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      setText(items[0]);
      return;
    }
    let line = 0;
    let char = 0;
    let deleting = false;
    let timer: number;
    const step = () => {
      const current = items[line];
      if (!deleting) {
        char++;
        setText(current.slice(0, char));
        setSpeaking(true);
        if (char >= current.length) {
          deleting = true;
          setSpeaking(false);
          timer = window.setTimeout(step, 2300);
          return;
        }
        timer = window.setTimeout(step, 42 + Math.random() * 40);
      } else {
        char--;
        setText(current.slice(0, char));
        if (char <= 0) {
          deleting = false;
          line = (line + 1) % items.length;
          timer = window.setTimeout(step, 350);
          return;
        }
        timer = window.setTimeout(step, 18);
      }
    };
    timer = window.setTimeout(step, 600);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items.join("|")]);

  return (
    <section id="bosh" className="relative isolate flex min-h-[100svh] items-center overflow-hidden pt-24 pb-16">
      <div className="bg-grid absolute inset-0 -z-10" />
      <div className="glow -z-10 top-10 -left-24 h-[28rem] w-[28rem] bg-brand" />
      <div className="glow -z-10 right-0 bottom-0 h-[32rem] w-[32rem] bg-brand-2" />

      <div className="container-x grid items-center gap-8 lg:grid-cols-[1.1fr_0.9fr] lg:gap-4">
        <div className="order-2 text-center lg:order-1 lg:text-left">
          <span className="section-eyebrow">
            <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-brand" />
            {badge}
          </span>
          <h1 className="min-h-[3.6em] font-display text-[2rem] leading-[1.18] font-semibold sm:min-h-[2.5em] sm:text-5xl lg:min-h-[3.6em] xl:text-6xl">
            <span className="sr-only">{items[0]}</span>
            <span aria-hidden className="caret text-gradient">
              {text}
            </span>
          </h1>
          <p className="mx-auto mt-5 max-w-xl text-base leading-relaxed text-mist/85 sm:text-lg lg:mx-0">{subtitle}</p>
          <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row lg:justify-start">
            <Link href="#dizaynerlar" className="btn-primary px-7 py-3.5 text-base">
              Dizaynerni tanlash
            </Link>
            <Link href="/login?tab=designer" className="btn-ghost px-7 py-3.5 text-base">
              Dizayner bo&apos;lib qo&apos;shilish
            </Link>
          </div>
        </div>

        <div className="relative order-1 mx-auto h-[300px] w-[270px] sm:h-[420px] sm:w-[370px] lg:order-2 lg:h-[560px] lg:w-[480px]">
          <div className="glass absolute top-[12%] left-0 z-10 hidden rounded-2xl rounded-br-sm px-4 py-2.5 text-sm text-mist shadow-xl sm:block lg:top-[14%] lg:left-2">
            {speaking ? (
              <span className="inline-flex gap-1">
                <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-brand [animation-delay:-0.3s]" />
                <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-brand [animation-delay:-0.15s]" />
                <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-brand" />
              </span>
            ) : (
              <>Salom! Men PP robotman 👋</>
            )}
          </div>
          <Robot speaking={speaking} />
        </div>
      </div>

      <a href="#dizaynerlar" className="absolute bottom-6 left-1/2 hidden -translate-x-1/2 flex-col items-center gap-2 text-xs text-muted sm:flex" aria-label="Pastga">
        <span className="h-10 w-6 rounded-full border border-white/20 p-1">
          <span className="block h-2 w-full animate-bounce rounded-full bg-brand" />
        </span>
      </a>
    </section>
  );
}
