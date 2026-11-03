import { Check, CheckCheck, ClipboardPlus, Download, FileText, MapPin, Reply } from "lucide-react";
import { Fragment, type ReactNode } from "react";
import { fileSize, formatTime } from "../lib/format";
import type { Message } from "../lib/types";
import { cx } from "./ui";

const URL_RE = /(https?:\/\/[^\s<]+)/g;

function linkify(text: string): ReactNode {
  const parts = text.split(URL_RE);
  return parts.map((part, i) =>
    i % 2 === 1 ? (
      <a key={i} href={part} target="_blank" rel="noreferrer noopener" className="break-all underline">
        {part}
      </a>
    ) : (
      <Fragment key={i}>{part}</Fragment>
    ),
  );
}

function Media({ m }: { m: Message }) {
  const src = `/api/media/${m.id}`;
  switch (m.media_type) {
    case "photo":
    case "sticker":
      return (
        <a href={src} target="_blank" rel="noreferrer" className="block">
          <img
            src={src}
            alt={m.media_name ?? "rasm"}
            loading="lazy"
            className={cx("rounded-lg object-cover", m.media_type === "sticker" ? "max-h-40 w-auto" : "max-h-80 w-full min-w-40 bg-slate-200")}
            onError={(e) => ((e.target as HTMLImageElement).style.display = "none")}
          />
        </a>
      );
    case "video":
    case "gif":
      return <video src={src} controls preload="none" className="max-h-80 w-full rounded-lg bg-black" />;
    case "voice":
    case "audio":
      return <audio src={src} controls preload="none" className="w-64 max-w-full" />;
    case "location":
      return (
        <div className="flex items-center gap-2 text-sm">
          <MapPin size={16} /> Joylashuv
        </div>
      );
    case "contact":
      return null;
    default:
      return (
        <a
          href={`${src}?download=1`}
          className="flex items-center gap-3 rounded-lg bg-black/5 p-2 pr-3 hover:bg-black/10"
          title="Yuklab olish"
        >
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-blue-600 text-white">
            <FileText size={18} />
          </span>
          <span className="min-w-0">
            <span className="block truncate text-sm font-medium">{m.media_name ?? "Fayl"}</span>
            <span className="text-xs opacity-60">{fileSize(m.media_size)}</span>
          </span>
          <Download size={16} className="ml-auto shrink-0 opacity-60" />
        </a>
      );
  }
}

export default function MessageBubble({
  m,
  read,
  senderLabel,
  replyText,
  onOrder,
  onReply,
}: {
  m: Message;
  read: boolean;
  senderLabel?: string | null;
  replyText?: string | null;
  onOrder?: () => void;
  onReply?: () => void;
}) {
  return (
    <div tabIndex={-1} className={cx("group flex items-end gap-1.5 outline-none", m.out ? "justify-end" : "justify-start")}>
      {m.out && (
        <BubbleActions onOrder={onOrder} onReply={onReply} />
      )}
      <div
        className={cx(
          "relative max-w-[85%] rounded-2xl px-3 py-2 shadow-sm sm:max-w-[70%]",
          m.out ? "rounded-br-md bg-blue-600 text-white" : "rounded-bl-md bg-white text-slate-900",
          m.deleted && "opacity-60",
        )}
      >
        {m.sender_name && !m.out && <div className="mb-0.5 text-xs font-semibold text-blue-600">{m.sender_name}</div>}
        {replyText && (
          <div className={cx("mb-1.5 border-l-2 pl-2 text-xs", m.out ? "border-white/60 text-white/80" : "border-blue-500 text-slate-500")}>
            <span className="line-clamp-2">{replyText}</span>
          </div>
        )}
        {m.media_type && (
          <div className={cx(m.text && "mb-1.5")}>
            <Media m={m} />
          </div>
        )}
        {m.text && <div className="whitespace-pre-wrap break-words text-[14.5px] leading-snug">{linkify(m.text)}</div>}
        <div className={cx("mt-1 flex items-center justify-end gap-1 text-[11px]", m.out ? "text-white/70" : "text-slate-400")}>
          {m.deleted && <span className="font-medium text-red-400">o'chirilgan</span>}
          {m.edited && !m.deleted && <span>tahrirlangan</span>}
          {senderLabel && <span className="truncate">{senderLabel} ·</span>}
          <span>{formatTime(m.date)}</span>
          {m.out && (read ? <CheckCheck size={14} /> : <Check size={14} />)}
        </div>
      </div>
      {!m.out && <BubbleActions onOrder={onOrder} onReply={onReply} />}
    </div>
  );
}

function BubbleActions({ onOrder, onReply }: { onOrder?: () => void; onReply?: () => void }) {
  return (
    <div className="pointer-events-none flex flex-col gap-1 opacity-0 transition group-hover:pointer-events-auto group-hover:opacity-100 group-focus:pointer-events-auto group-focus:opacity-100 group-focus-within:pointer-events-auto group-focus-within:opacity-100">
      {onReply && (
        <button onClick={onReply} className="rounded-full bg-white/90 p-1.5 text-slate-500 shadow-sm hover:text-blue-600" title="Javob berish">
          <Reply size={14} />
        </button>
      )}
      {onOrder && (
        <button onClick={onOrder} className="rounded-full bg-white/90 p-1.5 text-slate-500 shadow-sm hover:text-blue-600" title="Shu xabardan zakaz">
          <ClipboardPlus size={14} />
        </button>
      )}
    </div>
  );
}
