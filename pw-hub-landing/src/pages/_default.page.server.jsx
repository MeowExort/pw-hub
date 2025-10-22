// src/pages/_default.page.server.jsx
import React from 'react'
import {renderToString} from 'react-dom/server'
import {escapeInject, dangerouslySkipEscape} from 'vite-plugin-ssr/server'

export {render}
export const passToClient = ['pageProps']

import {PageShell} from '../PageShell'

async function render(pageContext) {
    const {Page, pageProps} = pageContext
    const pageHtml = renderToString(
        <PageShell pageContext={pageContext}>
            <Page {...pageProps} />
        </PageShell>
    )

    const {documentProps} = pageContext.exports || {}
    const title = (documentProps && documentProps.title) || 'PW Hub'
    const desc = (documentProps && documentProps.description) || 'Автоматизируйте управление аккаунтами Perfect World'

    const documentHtml = escapeInject`<!DOCTYPE html>
  <html lang="ru">
    <head>
      <meta charset="UTF-8" />
    
      <!-- Favicon -->
      <link rel="shortcut icon" href="/images/logo.jpg" />
      <link rel="apple-touch-icon" href="/images/logo.jpg" />
      <!-- Дополнительные иконки для разных устройств -->
      <link rel="icon" type="image/jpeg" href="/images/logo.jpg" sizes="32x32" />
      <link rel="icon" type="image/jpeg" href="/images/logo.jpg" sizes="192x192" />
      <link rel="apple-touch-icon" href="/images/logo.jpg" />
      
      <!-- Цвет браузера -->
      <meta name="msapplication-TileColor" content="#ffb300" />
      <meta name="theme-color" content="#ffb300" />
      
      <!-- Для Windows -->
      <meta name="msapplication-TileImage" content="/images/logo.jpg" />
      
      <meta name="viewport" content="width=device-width, initial-scale=1.0" />
      <title>${title}</title>
      <meta name="description" content="${desc}" />
      
      <!-- PWA -->
      <link rel="manifest" href="/manifest.json" />
      <meta name="theme-color" content="#ffb300" />
      
      <!-- Open Graph -->
      <meta property="og:title" content="${title}" />
      <meta property="og:description" content="${desc}" />
      <meta property="og:type" content="website" />
      <meta property="og:image" content="/og-image.jpg" />
      
      <!-- Twitter -->
      <meta name="twitter:card" content="summary_large_image" />
      <meta name="twitter:title" content="${title}" />
      <meta name="twitter:description" content="${desc}" />

      <!-- Шрифты -->
      <link rel="preconnect" href="https://fonts.googleapis.com">
      <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
      <link href="https://fonts.googleapis.com/css2?family=Orbitron:wght@400;500;700;900&family=Exo+2:wght@300;400;500;600;700&display=swap" rel="stylesheet">
    </head>
    <body>
      <div id="react-root">${dangerouslySkipEscape(pageHtml)}</div>
    </body>
  </html>`

    return {documentHtml}
}
