import type { MetadataRoute } from "next";
import { db } from "@/lib/db";
import { siteUrl } from "@/lib/env";

export const dynamic = "force-dynamic";

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const base = siteUrl();
  const designers = await db.designer
    .findMany({ where: { status: "APPROVED" }, select: { username: true, updatedAt: true } })
    .catch(() => []);
  return [
    { url: `${base}/`, changeFrequency: "weekly", priority: 1 },
    { url: `${base}/designers`, changeFrequency: "daily", priority: 0.8 },
    ...designers.map((d) => ({ url: `${base}/designer/${d.username}`, lastModified: d.updatedAt, changeFrequency: "weekly" as const, priority: 0.7 })),
  ];
}
