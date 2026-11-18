/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/pages/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/components/**/*.{js,ts,jsx,tsx,mdx}",
    "./src/app/**/*.{js,ts,jsx,tsx,mdx}",
  ],
  theme: {
    extend: {
      colors: {
        sigic: {
          canvas: "#08090a",
          surface1: "#0f1011",
          surface2: "#161718",
          hover: "#1c1e22",
          border: "#23252a",
          borderSubtle: "#18191c",
          accent: "#e4f222",
          accentHover: "#d3e015",
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
    },
  },
  plugins: [],
};
