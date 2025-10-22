import { useState, useEffect } from 'react'

export default function ScrollToTopButton() {
    const [isVisible, setIsVisible] = useState(false)

    // Показывать кнопку при скролле ниже 300px
    useEffect(() => {
        const toggleVisibility = () => {
            if (window.pageYOffset > 300) {
                setIsVisible(true)
            } else {
                setIsVisible(false)
            }
        }

        window.addEventListener('scroll', toggleVisibility)
        return () => window.removeEventListener('scroll', toggleVisibility)
    }, [])

    const scrollToTop = () => {
        window.scrollTo({
            top: 0,
            behavior: 'smooth'
        })
    }

    if (!isVisible) {
        return null
    }

    return (
        <button
            onClick={scrollToTop}
            style={{
                position: 'fixed',
                bottom: '2rem',
                right: '2rem',
                background: 'var(--accent)',
                color: 'var(--primary-bg)',
                border: 'none',
                borderRadius: '50%',
                width: '50px',
                height: '50px',
                fontSize: '1.5rem',
                cursor: 'pointer',
                boxShadow: '0 4px 12px rgba(255, 179, 0, 0.3)',
                zIndex: 1000,
                transition: 'all 0.3s ease',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center'
            }}
            onMouseEnter={(e) => {
                e.target.style.transform = 'scale(1.1)'
                e.target.style.background = 'var(--accent-hover)'
            }}
            onMouseLeave={(e) => {
                e.target.style.transform = 'scale(1)'
                e.target.style.background = 'var(--accent)'
            }}
            title="Прокрутить наверх"
        >
            ↑
        </button>
    )
}