import { cn } from "@/lib/utils";

/**
 * Uzluksiz aylanuvchi lenta. direction="right" — chapdan o'ngga harakatlanadi.
 * Sichqoncha ustiga borganda to'xtaydi (globals.css: .marquee:hover).
 */
export function Marquee({
  children,
  direction = "right",
  secondsPerItem = 6,
  count,
  className,
  gapClass = "gap-6",
}: {
  children: React.ReactNode[];
  direction?: "left" | "right";
  secondsPerItem?: number;
  count: number;
  className?: string;
  gapClass?: string;
}) {
  return (
    <div className={cn("marquee relative overflow-hidden", className)}>
      <div
        className={cn("marquee-track flex w-max py-4", direction === "right" ? "animate-marquee-right" : "animate-marquee-left")}
        style={{ ["--marquee-duration" as string]: `${Math.max(20, count * secondsPerItem)}s` }}
      >
        {[0, 1].map((copy) => (
          <div key={copy} className={cn("flex shrink-0 pr-6", gapClass)} aria-hidden={copy === 1 || undefined}>
            {children}
          </div>
        ))}
      </div>
    </div>
  );
}

/** Lenta bo'sh ko'rinmasligi uchun kam elementlarni takrorlaydi */
export function fillTo<T>(items: T[], min: number): T[] {
  if (!items.length) return items;
  const out = [...items];
  while (out.length < min) out.push(...items);
  return out;
}
