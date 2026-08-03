import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      manifest: {
        name: 'Elite Dangerous Dashboard',
        short_name: 'ED Dashboard',
        description: 'Real-time commander, fleet, economy, trade, colonisation, and Powerplay dashboard for Elite Dangerous.',
        theme_color: '#16171d',
        background_color: '#16171d',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: 'pwa-192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512.png', sizes: '512x512', type: 'image/png' },
          { src: 'pwa-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        // This is a live dashboard - never let the service worker serve a
        // stale cached response for API/SignalR traffic. Only the static
        // app shell (JS/CSS/icons) benefits from caching. Path-based (not
        // origin-based) since the API is same-origin in production but a
        // different port in dev - either way the path is what matters.
        runtimeCaching: [
          {
            urlPattern: ({ url }: { url: URL }) => url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/'),
            handler: 'NetworkOnly',
          },
        ],
      },
    }),
  ],
  server: {
    // Bind all interfaces so phones/tablets on the LAN can reach the dev server too.
    host: true,
    port: 5173,
  },
})
