import React from 'react'
import ReactDOM from 'react-dom/client'
// Initialise translations (and <html lang>) before anything renders.
import './i18n';
import App from './App.jsx'
import './index.css';
ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
)