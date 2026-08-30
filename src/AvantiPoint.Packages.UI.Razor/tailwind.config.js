/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./Components/**/*.{razor,html}",
    "./**/*.razor",
    "../host/AvantiPoint.Packages.Host/Pages/**/*.cshtml",
  ],
  darkMode: "media",
  theme: {
    extend: {
      colors: {
        page: "var(--nuget-page-bg)",
        panel: "var(--nuget-panel-bg)",
        surface: "var(--nuget-surface-bg)",
        border: "var(--nuget-border-color)",
        divider: "var(--nuget-divider-color)",
        brand: {
          DEFAULT: "var(--nuget-brand-color)",
          strong: "var(--nuget-brand-strong)",
        },
        fg: {
          DEFAULT: "var(--nuget-text-primary)",
          secondary: "var(--nuget-text-secondary)",
        },
        muted: "var(--nuget-muted)",
        header: {
          DEFAULT: "var(--site-header-bg)",
          fg: "var(--site-header-text)",
          border: "var(--site-nav-border)",
        },
        chip: {
          DEFAULT: "var(--chip-bg)",
          border: "var(--chip-border)",
          fg: "var(--chip-text)",
          accent: "var(--chip-accent-bg)",
          "accent-border": "var(--chip-accent-border)",
          "accent-fg": "var(--chip-accent-text)",
        },
        tag: {
          DEFAULT: "var(--tag-bg)",
          border: "var(--tag-border)",
          fg: "var(--tag-text)",
          more: "var(--tag-more-bg)",
          "more-fg": "var(--tag-more-text)",
        },
        pkg: {
          DEFAULT: "var(--pkg-bg)",
          border: "var(--pkg-border)",
          hover: "var(--pkg-hover)",
          title: "var(--pkg-title)",
          "title-hover": "var(--pkg-title-hover)",
          body: "var(--pkg-body-text)",
          icon: "var(--pkg-icon-bg)",
        },
      },
      boxShadow: {
        pkg: "var(--pkg-shadow)",
      },
      fontFamily: {
        sans: ['"Segoe UI"', "system-ui", "-apple-system", "sans-serif"],
        mono: ['"Cascadia Code"', '"Fira Code"', "Consolas", "ui-monospace", "monospace"],
      },
    },
  },
};
