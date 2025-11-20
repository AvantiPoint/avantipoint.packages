# AvantiPoint Packages Documentation Site

This directory contains the Docusaurus-based documentation site for AvantiPoint Packages.

## Structure

```
docs-site/
├── docs/              # All markdown documentation files
├── src/
│   ├── pages/         # React pages (home page)
│   └── css/           # Custom CSS
├── static/
│   ├── assets/        # Static assets (logo, favicon)
│   └── img/           # Images (banner, etc.)
├── docusaurus.config.ts
├── sidebars.ts
└── package.json
```

## Development

### Install Dependencies

```bash
npm install
```

### Start Development Server

```bash
npm start
```

This will start a local development server at `http://localhost:3000` with hot reloading.

### Build for Production

```bash
npm run build
```

This generates static content into the `build` directory.

### Serve Production Build

```bash
npm run serve
```

## Key Features

- **Modern React Home Page**: Beautiful landing page with hero section, feature highlights, use cases, and CTAs
- **Documentation**: All markdown docs from the main `docs/` folder organized by category
- **Custom Styling**: Brand colors and responsive design
- **SEO Optimized**: OpenGraph tags and Twitter card support

## Migration from MkDocs

This site was migrated from MkDocs to Docusaurus. Key changes:

- All docs moved from root `docs/` to `docs-site/docs/`
- `index.md` converted to a React component (`src/pages/index.tsx`)
- Assets moved to `static/` directory
- Navigation structure maintained via `sidebars.ts`
- Copyright dynamically generated (matches MkDocs behavior)
