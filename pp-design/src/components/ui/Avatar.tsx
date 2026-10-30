import { cn, initials } from "@/lib/utils";

export function Avatar({ src, name, className }: { src: string | null; name: string; className?: string }) {
  if (src) {
    // eslint-disable-next-line @next/next/no-img-element
    return <img src={src} alt={name} className={cn("rounded-full object-cover", className)} loading="lazy" decoding="async" referrerPolicy="no-referrer" />;
  }
  return (
    <span
      className={cn("inline-flex items-center justify-center rounded-full font-display font-semibold text-ink-950", className)}
      style={{ background: "linear-gradient(135deg, var(--color-brand), var(--color-brand-2) 60%, var(--color-brand-3))" }}
      aria-label={name}
    >
      {initials(name)}
    </span>
  );
}
