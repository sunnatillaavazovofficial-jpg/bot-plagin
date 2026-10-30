import type { Metadata, Viewport } from "next";
import { Manrope, Unbounded } from "next/font/google";
import { getSettings } from "@/lib/settings";
import { siteUrl } from "@/lib/env";
import "./globals.css";

const display = Unbounded({ subsets: ["latin", "cyrillic"], variable: "--font-unbounded", weight: ["400", "500", "600", "700"] });
const sans = Manrope({ subsets: ["latin", "cyrillic"], variable: "--font-manrope" });

export async function generateMetadata(): Promise<Metadata> {
  const settings = await getSettings();
  const name = settings.brand.name;
  return {
    metadataBase: new URL(siteUrl()),
    title: { default: `${name} — dizaynerlar platformasi`, template: `%s | ${name}` },
    description: settings.seo.description,
    applicationName: name,
    alternates: { canonical: "/" },
    openGraph: {
      type: "website",
      locale: "uz_UZ",
      siteName: name,
      title: `${name} — dizaynerlar platformasi`,
      description: settings.seo.description,
    },
    twitter: { card: "summary_large_image" },
    icons: { icon: "/logo.svg" },
  };
}

export const viewport: Viewport = {
  themeColor: "#0b1530",
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="uz" className={`${display.variable} ${sans.variable}`}>
      <body className="min-h-screen overflow-x-hidden">{children}</body>
    </html>
  );
}
