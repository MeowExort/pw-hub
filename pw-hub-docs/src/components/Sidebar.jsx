import { NavLink, useNavigate } from 'react-router-dom'

export default function Sidebar() {
    const navigate = useNavigate()

    const navItems = [
        { path: '/', label: '🏠 Введение', exact: true },
        { path: '/account', label: '👥 Account API' },
        { path: '/browser', label: '🌐 Browser API' },
        { path: '/utilities', label: '⚙️ Utilities' }
    ]

    const quickLinks = [
        { id: 'getting-started', label: '🚀 Начало работы' },
        { id: 'examples', label: '📚 Примеры скриптов' },
        { id: 'best-practices', label: '💡 Советы' }
    ]

    const handleQuickLinkClick = (anchorId) => {
        // Если мы уже на главной странице, просто скроллим к якорю
        if (window.location.pathname === '/') {
            const element = document.getElementById(anchorId)
            if (element) {
                element.scrollIntoView({ behavior: 'smooth' })
            }
        } else {
            // Если на другой странице, переходим на главную с якорем
            navigate(`/#${anchorId}`)
        }
    }

    return (
        <aside style={{
            width: 'var(--sidebar-width)',
            background: 'rgba(13, 20, 48, 0.8)',
            backdropFilter: 'blur(10px)',
            borderRight: '1px solid var(--border)',
            padding: '2rem 1rem',
            position: 'fixed',
            height: 'calc(100vh - 80px)',
            overflowY: 'auto',
            top: '80px'
        }}>
            <nav style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                {navItems.map(item => (
                    <NavLink
                        key={item.path}
                        to={item.path}
                        end={item.exact}
                        className={({ isActive }) =>
                            `nav-link ${isActive ? 'active' : ''}`
                        }
                        style={({ isActive }) => ({
                            color: isActive ? 'var(--accent)' : 'var(--text-secondary)',
                            textDecoration: 'none',
                            padding: '0.75rem 1rem',
                            borderRadius: '8px',
                            transition: 'all 0.3s ease',
                            background: isActive ? 'rgba(255, 179, 0, 0.1)' : 'transparent',
                            border: isActive ? '1px solid var(--accent)' : '1px solid transparent'
                        })}
                    >
                        {item.label}
                    </NavLink>
                ))}
            </nav>

            <div style={{ marginTop: '2rem', padding: '1rem' }}>
                <h4 style={{ color: 'var(--accent)', marginBottom: '1rem' }}>Быстрые ссылки</h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                    {quickLinks.map(link => (
                        <button
                            key={link.id}
                            onClick={() => handleQuickLinkClick(link.id)}
                            className="nav-link"
                            style={{
                                background: 'transparent',
                                border: 'none',
                                textAlign: 'left',
                                cursor: 'pointer',
                                color: 'var(--text-secondary)',
                                textDecoration: 'none',
                                padding: '0.75rem 1rem',
                                borderRadius: '8px',
                                transition: 'all 0.3s ease'
                            }}
                            onMouseOver={(e) => {
                                e.target.style.color = 'var(--accent)'
                                e.target.style.background = 'rgba(255, 179, 0, 0.1)'
                            }}
                            onMouseOut={(e) => {
                                e.target.style.color = 'var(--text-secondary)'
                                e.target.style.background = 'transparent'
                            }}
                        >
                            {link.label}
                        </button>
                    ))}
                </div>
            </div>
        </aside>
    )
}