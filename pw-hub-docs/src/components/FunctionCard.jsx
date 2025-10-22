import LuaCodeBlock from './LuaCodeBlock'

export default function FunctionCard({functionData}) {
    return (
        <div className="function-card" id={functionData.name} style={{
            scrollMarginTop: '100px' // Добавляем отступ для фиксированного хедера
        }}>
            <div style={{
                display: 'flex',
                alignItems: 'flex-start',
                justifyContent: 'space-between',
                marginBottom: '1rem'
            }}>
                <h3 style={{
                    color: 'var(--accent)',
                    margin: 0,
                    fontFamily: 'Courier New, monospace'
                }}>
                    {functionData.name}
                </h3>
                <span style={{
                    background: 'var(--accent)',
                    color: '#0d1430',
                    padding: '0.25rem 0.75rem',
                    borderRadius: '20px',
                    fontSize: '0.8rem',
                    fontWeight: 'bold'
                }}>
          {functionData.category}
        </span>
            </div>

            {/* Сигнатура функции с подсветкой синтаксиса */}
            <div style={{margin: '1rem 0'}}>
                <LuaCodeBlock code={functionData.signature}/>
            </div>

            <p style={{marginBottom: '1rem', color: 'var(--text-secondary)'}}>
                {functionData.description}
            </p>

            {functionData.parameters && functionData.parameters.length > 0 && (
                <div style={{marginBottom: '1rem'}}>
                    <h4 style={{color: 'var(--text-primary)', fontSize: '1.1rem', marginBottom: '0.5rem'}}>
                        Параметры:
                    </h4>
                    <div style={{display: 'grid', gap: '0.5rem'}}>
                        {functionData.parameters.map((param, index) => (
                            <div key={index} style={{
                                display: 'flex',
                                alignItems: 'flex-start',
                                gap: '1rem',
                                padding: '0.5rem',
                                background: 'rgba(255,255,255,0.05)',
                                borderRadius: '6px'
                            }}>
                                <code style={{
                                    color: 'var(--accent)',
                                    minWidth: '120px',
                                    fontFamily: 'Courier New, monospace'
                                }}>
                                    {param.name}
                                </code>
                                <span style={{color: 'var(--text-muted)', fontSize: '0.9rem'}}>
                  {param.type}
                </span>
                                <span style={{color: 'var(--text-secondary)', flex: 1}}>
                  {param.description}
                </span>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            <div style={{marginBottom: '1rem'}}>
                <h4 style={{color: 'var(--text-primary)', fontSize: '1.1rem', marginBottom: '0.5rem'}}>
                    Возвращаемое значение:
                </h4>
                <code style={{
                    color: 'var(--accent)',
                    fontFamily: 'Courier New, monospace',
                    background: 'rgba(255,179,0,0.1)',
                    padding: '0.25rem 0.5rem',
                    borderRadius: '4px'
                }}>
                    {functionData.returns}
                </code>
            </div>

            {functionData.example && (
                <div>
                    <h4 style={{color: 'var(--text-primary)', fontSize: '1.1rem', marginBottom: '0.5rem'}}>
                        Пример использования:
                    </h4>
                    <LuaCodeBlock code={functionData.example}/>
                </div>
            )}

            {functionData.notes && (
                <div style={{
                    marginTop: '1rem',
                    padding: '1rem',
                    background: 'rgba(255, 179, 0, 0.1)',
                    border: '1px solid var(--accent)',
                    borderRadius: '8px'
                }}>
                    <strong>💡 Примечание:</strong> {functionData.notes}
                </div>
            )}
        </div>
    )
}