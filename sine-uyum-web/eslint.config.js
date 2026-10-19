import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{js,jsx}'],
    extends: [
      js.configs.recommended,
      reactHooks.configs['recommended-latest'],
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
      parserOptions: {
        ecmaVersion: 'latest',
        ecmaFeatures: { jsx: true },
        sourceType: 'module',
      },
    },
    rules: {
      'no-unused-vars': ['error', { varsIgnorePattern: '^[A-Z_]' }],
      // Context dosyaları hem provider bileşenini hem de kendi hook'unu
      // (useAuth, useSnackbar vb.) export ediyor. Bu yaygın bir React
      // kalıbı; yalnızca hot reload'ın o dosyada tam yenileme yapmasına
      // yol açar, bir hata değil. CI'ı bloke etmemesi için uyarı seviyesinde.
      'react-refresh/only-export-components': 'warn',
    },
  },
])
