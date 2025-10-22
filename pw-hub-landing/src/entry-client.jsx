// src/entry-client.jsx
import React from 'react'
import { hydrateRoot } from 'react-dom/client'
import { PageShell } from './PageShell'
import './styles/globals.css'

export { render }

async function render(pageContext) {
    const { Page, pageProps } = pageContext
    const root = document.getElementById('react-root')
    hydrateRoot(
        root,
        <PageShell pageContext={pageContext}>
            <Page {...pageProps} />
        </PageShell>
    )
}