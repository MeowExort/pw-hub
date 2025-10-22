// vite.config.js
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import ssr from 'vite-plugin-ssr/plugin'

export default defineConfig({
    plugins: [
        react(),
        ssr({
            prerender: true
        })
    ],
    build: {
        target: 'esnext',
        minify: 'esbuild'
    },
    server: {
        port: 3000
    }
})