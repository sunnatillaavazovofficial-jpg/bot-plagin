import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const backend = process.env.BACKEND_URL ?? "http://localhost:8000";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: { outDir: "dist", sourcemap: false },
  server: {
    proxy: {
      "/api": { target: backend, changeOrigin: true },
      "/ws": { target: backend.replace("http", "ws"), ws: true, changeOrigin: true, headers: { origin: backend } },
    },
  },
});
