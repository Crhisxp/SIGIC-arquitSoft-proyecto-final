/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        sigic: {
          canvas: "#0f172a",
          surface1: "#0f1011",
          surface2: "#161718",
          surfaceHover: "#1c1e22",
          border: "#23252a",
          borderSubtle: "#18191c",
          accent: "#e4f222",
          accentHover: "#d3e015",
          accentMuted: "rgba(228, 242, 34, 0.14)",
          accentBorder: "rgba(228, 242, 34, 0.45)",
          accentFg: "#08090a",
          textPrimary: "#f7f8f8",
          textSecondary: "#8a8f98",
          textMuted: "#5e636e",
          stockOk: "#22c55e",
          stockWarn: "#f59e0b",
          stockDanger: "#ef4444",
          info: "#38bdf8",
        },
      },
      fontFamily: {
        sans: ["Inter", "-apple-system", "BlinkMacSystemFont", "Segoe UI", "Roboto", "sans-serif"],
        mono: ["JetBrains Mono", "Fira Code", "Cascadia Code", "monospace"],
      },
    },
  },
  plugins: [],
};
