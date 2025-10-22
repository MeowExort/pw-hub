import { luaApiData } from '../data/luaApiData'
import FunctionCard from '../components/FunctionCard'
import {useMemo, useState} from "react";
import SearchBox from '../components/SearchBox'

export default function Utilities() {
    const [searchQuery, setSearchQuery] = useState('')

    const filteredFunctions = useMemo(() => {
        if (!searchQuery.trim()) return luaApiData.utilities

        const query = searchQuery.toLowerCase()
        return luaApiData.utilities.filter(func =>
            func.name.toLowerCase().includes(query) ||
            func.description.toLowerCase().includes(query) ||
            func.signature.toLowerCase().includes(query) ||
            (func.parameters && func.parameters.some(param =>
                param.name.toLowerCase().includes(query) ||
                param.description.toLowerCase().includes(query)
            ))
        )
    }, [searchQuery])

    const handleLocalSearch = (results, query) => {
        setSearchQuery(query)
    }
    
    return (
        <div>
            <h1>⚙️ Utilities</h1>
            <p style={{ fontSize: '1.1rem', color: 'var(--text-secondary)', marginBottom: '2rem' }}>
                Вспомогательные функции для отладки, логирования и управления выполнением скриптов.
            </p>

            <section style={{ marginBottom: '2rem' }}>
                <h2>Утилиты разработки</h2>
                <p>
                    Эти функции помогают в создании надежных и удобных скриптов, предоставляя инструменты
                    для отладки, управления временем и отслеживания прогресса.
                </p>
            </section>

            {/* Локальный поиск для этой страницы */}
            <div style={{ marginBottom: '2rem' }}>
                <SearchBox onSearch={handleLocalSearch} />
                {searchQuery && (
                    <div style={{
                        color: 'var(--text-muted)',
                        fontSize: '0.9rem',
                        marginTop: '0.5rem'
                    }}>
                        Найдено функций: {filteredFunctions.length}
                        {searchQuery && ` по запросу "${searchQuery}"`}
                    </div>
                )}
            </div>

            <section>
                <h2>Функции</h2>
                {filteredFunctions.map(func => (
                    <FunctionCard key={func.name} functionData={func} />
                ))}

                {filteredFunctions.length === 0 && searchQuery && (
                    <div style={{
                        textAlign: 'center',
                        padding: '3rem',
                        color: 'var(--text-muted)',
                        background: 'rgba(255,255,255,0.05)',
                        borderRadius: '12px'
                    }}>
                        🔍 Не найдено функций Utilities API для "{searchQuery}"
                    </div>
                )}
            </section>
        </div>
    )
}