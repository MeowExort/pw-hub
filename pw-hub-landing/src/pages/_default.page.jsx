// src/pages/_default.page.jsx
import React from 'react'

export { PageShell }

function PageShell({ children }) {
    return (
        <React.StrictMode>
            <div id="page-root">
                {children}
            </div>
        </React.StrictMode>
    )
}