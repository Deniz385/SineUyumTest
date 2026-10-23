import React from 'react'
import ReactDOM from 'react-dom/client'
// Fonts are bundled with the app (no font CDN). The browser only downloads the
// subsets a page needs (latin, latin-ext for Turkish...).
import '@fontsource-variable/inter';
import '@fontsource-variable/bricolage-grotesque';
// Initialise translations (and <html lang>) before anything renders.
import './i18n';
import App from './App.jsx'
import './index.css';
ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)
