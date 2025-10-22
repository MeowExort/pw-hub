// src/PageLayout.jsx
import React from 'react'
import './styles/globals.css'

export function PageLayout({ children }) {
    return (
        <React.StrictMode>
            {children}
        </React.StrictMode>
    )
}