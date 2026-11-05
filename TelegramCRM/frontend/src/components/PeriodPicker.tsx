import { PERIOD_PRESETS } from "../lib/periods";
import { Input, cx } from "./ui";

export default function PeriodPicker({
  preset,
  range,
  onChange,
}: {
  preset: string;
  range: [string, string];
  onChange: (preset: string, range: [string, string]) => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      {PERIOD_PRESETS.map((p) => (
        <button
          key={p.key}
          onClick={() => onChange(p.key, p.range())}
          className={cx(
            "rounded-full px-3 py-1.5 text-sm font-medium transition",
            preset === p.key ? "bg-slate-900 text-white" : "bg-white text-slate-600 ring-1 ring-slate-200 hover:bg-slate-50",
          )}
        >
          {p.label}
        </button>
      ))}
      <div className="flex items-center gap-1.5">
        <Input
          type="date"
          value={range[0]}
          max={range[1]}
          onChange={(e) => onChange("custom", [e.target.value, range[1]])}
          className="h-9 w-auto"
          aria-label="Boshlanish sanasi"
        />
        <span className="text-slate-400">—</span>
        <Input
          type="date"
          value={range[1]}
          min={range[0]}
          onChange={(e) => onChange("custom", [range[0], e.target.value])}
          className="h-9 w-auto"
          aria-label="Tugash sanasi"
        />
      </div>
    </div>
  );
}
