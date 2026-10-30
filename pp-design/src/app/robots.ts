import type { MetadataRoute } from "next";
import { siteUrl } from "@/lib/env";

export const dynamic = "force-dynamic";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: { userAgent: "*", allow: "/", disallow: ["/admin", "/dashboard", "/api/", "/login"] },
    sitemap: `${siteUrl()}/sitemap.xml`,
  };
}
