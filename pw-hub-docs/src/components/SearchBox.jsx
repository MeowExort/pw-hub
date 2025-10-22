import { useState, useEffect, useRef } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { getAllFunctions } from '../data/luaApiData'

export default function SearchBox({ onSearch }) {
    const [query, setQuery] = useState('')
    const [results, setResults] = useState([])
    const [isOpen, setIsOpen] = useState(false)
    const searchRef = useRef(null)
    const navigate = useNavigate()
    const location = useLocation()

    // Закрытие результатов при клике вне компонента
    useEffect(() => {
        function handleClickOutside(event) {
            if (searchRef.current && !searchRef.current.contains(event.target)) {
                setIsOpen(false)
            }
        }

        document.addEventListener('mousedown', handleClickOutside)
        return () => document.removeEventListener('mousedown', handleClickOutside)
    }, [])

    // Закрытие результатов при изменении маршрута
    useEffect(() => {
        setIsOpen(false)
    }, [location.pathname])

    // Поиск по всем функциям
    const performSearch = (searchQuery) => {
        if (!searchQuery.trim()) {
            setResults([])
            setIsOpen(false)
            return
        }

        const searchTerm = searchQuery.toLowerCase()
        const allFunctions = getAllFunctions()

        const found = allFunctions.filter(func =>
            func.name.toLowerCase().includes(searchTerm) ||
            func.description.toLowerCase().includes(searchTerm) ||
            func.signature.toLowerCase().includes(searchTerm) ||
            (func.parameters && func.parameters.some(param =>
                param.name.toLowerCase().includes(searchTerm) ||
                param.description.toLowerCase().includes(searchTerm)
            ))
        )

        setResults(found)
        setIsOpen(true)

        if (onSearch) {
            onSearch(found, searchQuery)
        }
    }

    const handleInputChange = (e) => {
        const value = e.target.value
        setQuery(value)
        performSearch(value)
    }

    const handleResultClick = (func) => {
        // Переходим на нужную страницу
        navigate(`/${func.page}#${func.name}`)

        // Закрываем поиск и очищаем запрос
        setIsOpen(false)
        setQuery('')
    }

    const clearSearch = () => {
        setQuery('')
        setResults([])
        setIsOpen(false)
    }

    const handleKeyDown = (e) => {
        if (e.key === 'Escape') {
            clearSearch()
        } else if (e.key === 'Enter' && results.length > 0 && isOpen) {
            // При нажатии Enter выбираем первый результат
            handleResultClick(results[0])
        }
    }

    return (
        <div ref={searchRef} style={{ position: 'relative', width: '100%', maxWidth: '400px' }}>
            <div style={{ position: 'relative' }}>
                <input
                    type="text"
                    value={query}
                    onChange={handleInputChange}
                    onKeyDown={handleKeyDown}
                    placeholder="🔍 Поиск функций (название, описание, параметры)..."
                    style={{
                        width: '100%',
                        padding: '0.75rem 1rem',
                        paddingRight: '2.5rem',
                        background: 'rgba(255, 255, 255, 0.1)',
                        border: '1px solid var(--border)',
                        borderRadius: '8px',
                        color: 'var(--text-primary)',
                        fontSize: '0.9rem',
                        backdropFilter: 'blur(10px)',
                        transition: 'all 0.3s ease'
                    }}
                    onFocus={() => query && setIsOpen(true)}
                />

                {query && (
                    <button
                        onClick={clearSearch}
                        style={{
                            position: 'absolute',
                            right: '0.5rem',
                            top: '50%',
                            transform: 'translateY(-50%)',
                            background: 'none',
                            border: 'none',
                            color: 'var(--text-muted)',
                            cursor: 'pointer',
                            fontSize: '1.2rem',
                            padding: '0.25rem',
                            borderRadius: '4px',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center'
                        }}
                        onMouseOver={(e) => {
                            e.target.style.background = 'rgba(255, 255, 255, 0.1)'
                        }}
                        onMouseOut={(e) => {
                            e.target.style.background = 'none'
                        }}
                    >
                        ×
                    </button>
                )}
            </div>

            {/* Результаты поиска */}
            {isOpen && results.length > 0 && (
                <div style={{
                    position: 'absolute',
                    top: '100%',
                    left: 0,
                    right: 0,
                    background: 'var(--primary-bg)',
                    border: '1px solid var(--border)',
                    borderRadius: '8px',
                    marginTop: '0.5rem',
                    maxHeight: '400px',
                    overflowY: 'auto',
                    zIndex: 1000,
                    boxShadow: '0 8px 25px rgba(0, 0, 0, 0.5)'
                }}>
                    {results.map((func, index) => (
                        <div
                            key={func.name + index}
                            onClick={() => handleResultClick(func)}
                            style={{
                                padding: '1rem',
                                borderBottom: '1px solid var(--border)',
                                cursor: 'pointer',
                                transition: 'all 0.2s ease',
                                background: 'rgba(255, 255, 255, 0.02)'
                            }}
                            onMouseEnter={(e) => {
                                e.target.style.background = 'rgba(255, 179, 0, 0.1)'
                            }}
                            onMouseLeave={(e) => {
                                e.target.style.background = 'rgba(255, 255, 255, 0.02)'
                            }}
                        >
                            <div style={{
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'space-between',
                                marginBottom: '0.5rem'
                            }}>
                                <code style={{
                                    color: 'var(--accent)',
                                    fontWeight: 'bold',
                                    fontSize: '0.9rem'
                                }}>
                                    {func.name}
                                </code>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                  <span style={{
                      background: 'var(--accent)',
                      color: 'var(--primary-bg)',
                      padding: '0.2rem 0.5rem',
                      borderRadius: '12px',
                      fontSize: '0.7rem',
                      fontWeight: 'bold'
                  }}>
                    {func.category}
                  </span>
                                    <span style={{
                                        background: 'rgba(0, 150, 255, 0.3)',
                                        color: '#0096ff',
                                        padding: '0.2rem 0.5rem',
                                        borderRadius: '12px',
                                        fontSize: '0.7rem',
                                        fontWeight: 'bold'
                                    }}>
                    {func.page}
                  </span>
                                </div>
                            </div>

                            <div style={{
                                color: 'var(--text-secondary)',
                                fontSize: '0.8rem',
                                marginBottom: '0.5rem'
                            }}>
                                {func.description.length > 100
                                    ? func.description.substring(0, 100) + '...'
                                    : func.description
                                }
                            </div>

                            <div style={{
                                color: 'var(--text-muted)',
                                fontSize: '0.75rem',
                                fontFamily: 'Courier New, monospace'
                            }}>
                                {func.signature}
                            </div>
                        </div>
                    ))}
                </div>
            )}

            {/* Сообщение "ничего не найдено" */}
            {isOpen && query && results.length === 0 && (
                <div style={{
                    position: 'absolute',
                    top: '100%',
                    left: 0,
                    right: 0,
                    background: 'var(--primary-bg)',
                    border: '1px solid var(--border)',
                    borderRadius: '8px',
                    marginTop: '0.5rem',
                    padding: '1.5rem',
                    textAlign: 'center',
                    color: 'var(--text-muted)'
                }}>
                    🔍 Ничего не найдено для "{query}"
                    <div style={{ fontSize: '0.8rem', marginTop: '0.5rem' }}>
                        Попробуйте другие ключевые слова
                    </div>
                </div>
            )}
        </div>
    )
}