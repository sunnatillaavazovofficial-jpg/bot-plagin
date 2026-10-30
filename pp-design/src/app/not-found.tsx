import Link from "next/link";
import { LogoMark } from "@/components/Logo";

export default function NotFound() {
  return (
    <div className="relative flex min-h-screen flex-col items-center justify-center overflow-hidden px-6 text-center">
      <div className="bg-grid absolute inset-0 -z-10" />
      <div className="glow -z-10 top-1/4 left-1/3 h-80 w-80 bg-brand-2" />
      <LogoMark className="mb-6 h-16 w-16" />
      <p className="text-gradient font-display text-7xl font-semibold">404</p>
      <h1 className="mt-4 font-display text-2xl font-semibold">Sahifa topilmadi</h1>
      <p className="mt-2 text-mist/70">Bu sahifa mavjud emas yoki ko&apos;chirilgan.</p>
      <Link href="/" className="btn-primary mt-8">
        Bosh sahifaga qaytish
      </Link>
    </div>
  );
}
