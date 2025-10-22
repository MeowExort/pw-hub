# Changelog — pw-hub-landing (Vite + vite-plugin-ssr)

All notable changes to the landing site will be documented in this file.

## [2025-10-23]
### Fixed
- Resolved SSR build errors by:
  - Removing CSS imports from server entry and default layout (SSR-safe).
  - Adding client-side render hook `_default.page.client.jsx` with `render()`.
  - Adding server-side render hook `_default.page.server.jsx` with `render()` returning `documentHtml` via `escapeInject`.
  - Whitelisting `pageProps` in `passToClient` to make client access legal.

### Added
- Benefits section now fetches real stats from `/api/app/stats` and displays active users and completed modules with compact number formatting.
- Added Dockerfile for multi-stage build and Nginx static hosting of `dist`.

### Notes
- Public assets references simplified (`/og-image.jpg`, `/hero-screenshot.png`).
- Ensure reverse proxy routes `/api` to Pw.Modules.Api or enable CORS.
