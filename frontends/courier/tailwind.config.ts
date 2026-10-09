import type { Config } from "tailwindcss";

const config: Config = {
  darkMode: ["class"],
  content: ["./src/**/*.{ts,tsx,mdx}"],
  theme: {
    container: {
      center: true,
      padding: { DEFAULT: "1rem", md: "1.5rem", lg: "2rem" },
      screens: { "2xl": "1280px" },
    },
    extend: {
      colors: {
        // Brand
        brand: {
          DEFAULT: "#534AB7",
          50: "#F2F1FB",
          100: "#E4E2F6",
          200: "#C9C5ED",
          300: "#A29CDE",
          400: "#7B72CB",
          500: "#534AB7",
          600: "#433C95",
          700: "#332E73",
          800: "#262252",
          900: "#1A1736",
        },
        accent: {
          DEFAULT: "#D85A30",
          50: "#FBEFEA",
          100: "#F6DBCE",
          200: "#EEB59E",
          300: "#E68F6D",
          400: "#D85A30",
          500: "#C04A21",
          600: "#9B3A1A",
          700: "#762C14",
          800: "#511E0D",
          900: "#2D1107",
        },
        // Online (verde — Conectarme, Entregar) y peligro (rojo — Desconectarme)
        success: {
          DEFAULT: "#1D9E75",
          50: "#E7F7F0",
          100: "#C5EBD9",
          500: "#1D9E75",
          600: "#16805E",
          700: "#0F6448",
        },
        reject: {
          DEFAULT: "#A32D2D",
          50: "#FBEAEA",
          100: "#F4CACA",
          500: "#A32D2D",
          600: "#852424",
          700: "#651A1A",
        },
        // Mismo set de status del merchant para badges genericos
        status: {
          delivered: "#16A34A",
          ontheway: "#D97706",
          preparing: "#2563EB",
          cancelled: "#DC2626",
          pending: "#534AB7",
        },
        // shadcn neutrals
        border: "hsl(240 5.9% 90%)",
        input: "hsl(240 5.9% 90%)",
        ring: "hsl(245 47% 50%)",
        background: "#FAFAF7",
        foreground: "hsl(240 10% 3.9%)",
        muted: { DEFAULT: "hsl(240 4.8% 95.9%)", foreground: "hsl(240 3.8% 46.1%)" },
        card: { DEFAULT: "#FFFFFF", foreground: "hsl(240 10% 3.9%)" },
        popover: { DEFAULT: "#FFFFFF", foreground: "hsl(240 10% 3.9%)" },
        destructive: { DEFAULT: "#DC2626", foreground: "#FFFFFF" },
      },
      borderRadius: {
        sm: "8px",
        md: "10px",
        lg: "12px",
        xl: "16px",
        "2xl": "20px",
      },
      fontFamily: {
        sans: ["var(--font-geist-sans)", "Inter", "system-ui", "sans-serif"],
        mono: ["var(--font-geist-mono)", "monospace"],
      },
      keyframes: {
        "accordion-down": { from: { height: "0" }, to: { height: "var(--radix-accordion-content-height)" } },
        "accordion-up":   { from: { height: "var(--radix-accordion-content-height)" }, to: { height: "0" } },
        "fade-in":        { from: { opacity: "0" }, to: { opacity: "1" } },
        "slide-up":       { from: { transform: "translateY(8px)", opacity: "0" }, to: { transform: "translateY(0)", opacity: "1" } },
        "pulse-online":   { "0%, 100%": { opacity: "1" }, "50%": { opacity: "0.6" } },
      },
      animation: {
        "accordion-down": "accordion-down 0.2s ease-out",
        "accordion-up": "accordion-up 0.2s ease-out",
        "fade-in": "fade-in .25s ease-out",
        "slide-up": "slide-up .25s ease-out",
        "pulse-online": "pulse-online 2s ease-in-out infinite",
      },
    },
  },
  plugins: [require("tailwindcss-animate")],
};

export default config;
