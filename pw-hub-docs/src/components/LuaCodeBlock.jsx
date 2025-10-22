import { useState, useEffect } from 'react'
import Editor from 'react-simple-code-editor'
import { highlight, languages } from 'prismjs'
import 'prismjs/components/prism-lua'
import 'prismjs/themes/prism-tomorrow.css'

export default function LuaCodeBlock({ code, readOnly = true }) {
    const [value, setValue] = useState(code)

    useEffect(() => {
        setValue(code)
    }, [code])

    const highlightCode = (code) =>
        highlight(code, languages.lua, 'lua')

    if (readOnly) {
        return (
            <div style={{
                background: '#2d2d2d',
                border: '1px solid #444',
                borderRadius: '8px',
                padding: '1rem',
                margin: '1rem 0',
                overflow: 'auto',
                fontSize: '0.9rem',
                lineHeight: '1.4'
            }}>
        <pre style={{ margin: 0, fontFamily: '"Fira Code", "Courier New", monospace' }}>
          <code
              dangerouslySetInnerHTML={{
                  __html: highlightCode(code)
              }}
              style={{
                  fontFamily: '"Fira Code", "Courier New", monospace'
              }}
          />
        </pre>
            </div>
        )
    }

    return (
        <div style={{
            background: '#2d2d2d',
            border: '1px solid #444',
            borderRadius: '8px',
            margin: '1rem 0',
            overflow: 'hidden'
        }}>
            <Editor
                value={value}
                onValueChange={setValue}
                highlight={highlightCode}
                padding={16}
                style={{
                    fontFamily: '"Fira Code", "Courier New", monospace',
                    fontSize: '0.9rem',
                    lineHeight: '1.4',
                    minHeight: '200px'
                }}
                textareaClassName="code-editor-textarea"
                preClassName="code-editor-pre"
            />
        </div>
    )
}