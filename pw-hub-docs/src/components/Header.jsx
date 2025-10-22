import { Link } from 'react-router-dom'
import SearchBox from './SearchBox'

export default function Header() {
    return (
        <header className="header" style={{
            background: 'rgba(13, 20, 48, 0.9)',
            backdropFilter: 'blur(10px)',
            borderBottom: '1px solid var(--border)',
            padding: '1rem 2rem',
            position: 'sticky',
            top: 0,
            zIndex: 100
        }}>
            <div style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                maxWidth: '1400px',
                margin: '0 auto',
                gap: '2rem'
            }}>
                {/* Логотип и название */}
                <Link to="/" style={{ textDecoration: 'none', flexShrink: 0 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                        <div style={{
                            width: '40px',
                            height: '40px',
                            background: 'linear-gradient(45deg, var(--accent), #ff8f00)',
                            borderRadius: '10px',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            fontWeight: 'bold',
                            color: '#0d1430',
                            fontSize: '1.2rem'
                        }}>
                            PW
                        </div>
                        <div>
                            <h1 style={{
                                fontSize: '1.5rem',
                                margin: 0,
                                background: 'linear-gradient(45deg, var(--accent), #ff8f00)',
                                WebkitBackgroundClip: 'text',
                                WebkitTextFillColor: 'transparent'
                            }}>
                                PW Hub Docs
                            </h1>
                            <p style={{
                                color: 'var(--text-muted)',
                                fontSize: '0.9rem',
                                margin: 0
                            }}>
                                Lua API Documentation
                            </p>
                        </div>
                    </div>
                </Link>

                {/* Поиск */}
                <div style={{ flex: 1, maxWidth: '500px' }}>
                    <SearchBox />
                </div>

                {/* Ссылка на главный сайт */}
                <nav style={{ flexShrink: 0 }}>
                    <a
                        href="https://pw-hub.ru"
                        target="_blank"
                        rel="noopener noreferrer"
                        style={{
                            color: 'var(--accent)',
                            textDecoration: 'none',
                            padding: '0.5rem 1rem',
                            border: '1px solid var(--accent)',
                            borderRadius: '6px',
                            transition: 'all 0.3s ease',
                            whiteSpace: 'nowrap'
                        }}
                        onMouseOver={(e) => {
                            e.target.style.background = 'var(--accent)';
                            e.target.style.color = '#0d1430';
                        }}
                        onMouseOut={(e) => {
                            e.target.style.background = 'transparent';
                            e.target.style.color = 'var(--accent)';
                        }}
                    >
                        🎮 Главный сайт
                    </a>
                </nav>
            </div>
        </header>
    )
}