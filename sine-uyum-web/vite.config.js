import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        // Kütüphaneler ayrı parçalarda: uygulama kodu değişince tarayıcı
        // önbelleğindeki React/MUI yeniden indirilmez.
        manualChunks(id) {
          if (!id.includes('node_modules')) return undefined;
          if (id.includes('@microsoft/signalr')) return 'signalr';
          if (id.includes('@mui') || id.includes('@emotion')) return 'mui';
          if (/[\\/]node_modules[\\/](react|react-dom|react-router|react-router-dom|scheduler)[\\/]/.test(id)) return 'react';
          return 'vendor';
        },
      },
    },
  },
})
