// src/PageShell.jsx
import React from 'react'

export function PageShell({ children, pageContext }) {
    return (
        <React.StrictMode>
            {children}
        </React.StrictMode>
    )
}