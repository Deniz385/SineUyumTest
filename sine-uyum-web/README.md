# React + Vite

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Babel](https://babeljs.io/) for Fast Refresh
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/) for Fast Refresh

## Expanding the ESLint configuration

If you are developing a production application, we recommend using TypeScript with type-aware lint rules enabled. Check out the [TS template](https://github.com/vitejs/vite/tree/main/packages/create-vite/template-react-ts) for information on how to integrate TypeScript and [`typescript-eslint`](https://typescript-eslint.io) in your project.

## Translations

The UI uses [i18next](https://www.i18next.com/) with `react-i18next`. Strings live in
`src/locales/<code>.json` (nested keys grouped by feature, e.g. `nav.feed`, `reviews.spoilerReveal`).

To add a language:

1. Copy `src/locales/en.json` to `src/locales/<code>.json` and translate the values
   (keep the keys and `{{placeholders}}`; plural keys use the `_one` / `_other` suffixes).
2. Add `{ code: '<code>', label: '<name in that language>' }` to `SUPPORTED_LANGUAGES` in `src/i18n.js`.

The initial language is the saved choice, then the browser language, then English. Every API request
sends `Accept-Language`, and the server is told the user's language via `PUT /api/v1/account/language`.
