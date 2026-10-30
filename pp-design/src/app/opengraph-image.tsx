import { ImageResponse } from "next/og";

export const alt = "PP Design — dizaynerlar platformasi";
export const size = { width: 1200, height: 630 };
export const contentType = "image/png";

/** Ijtimoiy tarmoqlarda havola ulashilganda chiqadigan rasm */
export default function OpengraphImage() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          padding: 80,
          background: "radial-gradient(circle at 20% 20%, #1d4f8f 0%, #0b1530 45%), #0b1530",
          color: "white",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 24 }}>
          <div
            style={{
              width: 96,
              height: 96,
              borderRadius: 28,
              border: "3px solid #38c8ff",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 52,
              fontWeight: 800,
              color: "#38c8ff",
              background: "#122045",
            }}
          >
            PP
          </div>
          <div style={{ fontSize: 56, fontWeight: 800 }}>PP Design</div>
        </div>
        <div style={{ marginTop: 48, fontSize: 64, fontWeight: 800, lineHeight: 1.1, maxWidth: 950, display: "flex" }}>
          Dizayn — g&apos;or devoridan sun&apos;iy intellektgacha
        </div>
        <div style={{ marginTop: 28, fontSize: 30, color: "#c7d2f0", display: "flex" }}>Eng yaxshi dizaynerlar — bitta platformada</div>
        <div style={{ position: "absolute", right: 0, bottom: 0, width: 520, height: 14, background: "linear-gradient(90deg, #38c8ff, #7b61ff, #ff5ccd)" }} />
      </div>
    ),
    size,
  );
}
