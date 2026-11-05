import { todayISO } from "./format";

function monthStart(offset = 0): string {
  const d = new Date();
  d.setDate(1);
  d.setMonth(d.getMonth() + offset);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
}

function monthEnd(offset: number): string {
  const d = new Date();
  d.setDate(1);
  d.setMonth(d.getMonth() + offset + 1);
  d.setDate(0);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

export const PERIOD_PRESETS: { key: string; label: string; range: () => [string, string] }[] = [
  { key: "today", label: "Bugun", range: () => [todayISO(), todayISO()] },
  { key: "month", label: "Bu oy", range: () => [monthStart(0), todayISO()] },
  { key: "prev", label: "O'tgan oy", range: () => [monthStart(-1), monthEnd(-1)] },
  { key: "7", label: "7 kun", range: () => [todayISO(-6), todayISO()] },
  { key: "30", label: "30 kun", range: () => [todayISO(-29), todayISO()] },
  { key: "90", label: "90 kun", range: () => [todayISO(-89), todayISO()] },
  { key: "year", label: "Bu yil", range: () => [`${new Date().getFullYear()}-01-01`, todayISO()] },
];

export function defaultPeriod(): [string, string] {
  return [monthStart(0), todayISO()];
}
