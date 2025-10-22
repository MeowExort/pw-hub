import { luaApiData } from '../data/luaApiData'
import FunctionCard from '../components/FunctionCard'
import {useMemo, useState} from "react";
import SearchBox from '../components/SearchBox'

export default function BrowserApi() {
    const [searchQuery, setSearchQuery] = useState('')

    const filteredFunctions = useMemo(() => {
        if (!searchQuery.trim()) return luaApiData.browser

        const query = searchQuery.toLowerCase()
        return luaApiData.browser.filter(func =>
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
            <h1>🌐 Browser API</h1>
            <p style={{ fontSize: '1.1rem', color: 'var(--text-secondary)', marginBottom: '2rem' }}>
                Функции для автоматизации веб-браузера: навигация, выполнение скриптов, работа с DOM.
            </p>

            <section style={{ marginBottom: '2rem' }}>
                <h2>Особенности</h2>
                <ul style={{ paddingLeft: '1.5rem', color: 'var(--text-secondary)' }}>
                    <li>Поддержка только домена pwonline.ru</li>
                    <li>Асинхронное выполнение всех операций</li>
                    <li>Встроенная обработка ошибок</li>
                    <li>Поддержка современных веб-стандартов</li>
                </ul>
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
                        🔍 Не найдено функций Browser API для "{searchQuery}"
                    </div>
                )}
            </section>
        </div>
    )
}