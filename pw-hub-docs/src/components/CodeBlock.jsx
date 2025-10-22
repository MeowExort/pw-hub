import { useState } from 'react'

export default function CodeBlock({ code, language = 'lua' }) {
    const [copied, setCopied] = useState(false)

    const copyToClipboard = async () => {
        try {
            await navigator.clipboard.writeText(code)
            setCopied(true)
            setTimeout(() => setCopied(false), 2000)
        } catch (err) {
            console.error('Failed to copy text: ', err)
        }
    }

    return (
        <div style={{ position: 'relative' }}>
            <div className="code-block">
        <pre>
          <code>{code}</code>
        </pre>
            </div>
            <button
                onClick={copyToClipboard}
                style={{
                    position: 'absolute',
                    top: '0.5rem',
                    right: '0.5rem',
                    background: copied ? '#4CAF50' : 'var(--accent)',
                    color: '#0d1430',
                    border: 'none',
                    padding: '0.25rem 0.75rem',
                    borderRadius: '4px',
                    fontSize: '0.8rem',
                    fontWeight: 'bold',
                    cursor: 'pointer',
                    transition: 'all 0.3s ease'
                }}
            >
                {copied ? '✅ Скопировано!' : '📋 Копировать'}
            </button>
        </div>
    )
}