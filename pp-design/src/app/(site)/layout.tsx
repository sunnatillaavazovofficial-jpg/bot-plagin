import { Footer } from "@/components/Footer";
import { Navbar, type NavUser } from "@/components/Navbar";
import { getCurrentUser } from "@/lib/auth";
import { getSettings } from "@/lib/settings";

export default async function SiteLayout({ children }: { children: React.ReactNode }) {
  const [settings, user] = await Promise.all([getSettings(), getCurrentUser()]);
  const navUser: NavUser = user
    ? { name: user.designer?.fullName || user.name || (user.role === "ADMIN" ? "Admin" : "Mening hisobim"), role: user.role }
    : null;
  return (
    <>
      <Navbar brandName={settings.brand.name} logoUrl={settings.brand.logoUrl} user={navUser} />
      <main>{children}</main>
      <Footer settings={settings} />
    </>
  );
}
