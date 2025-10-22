import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

export default function ScrollToTop({
                                        behavior = 'smooth',
                                        top = 0,
                                        left = 0
                                    }) {
    const { pathname, hash } = useLocation()

    useEffect(() => {
        // Если есть хэш, прокручиваем к элементу на любой странице
        if (hash) {
            const id = hash.replace('#', '')
            const element = document.getElementById(id)

            if (element) {
                // Задержка для гарантии, что DOM обновлен
                setTimeout(() => {
                    element.scrollIntoView({
                        behavior: behavior,
                        block: 'start'
                    })

                    // Добавляем временную подсветку
                    element.style.transition = 'all 0.5s ease'
                    element.style.background = 'rgba(255, 179, 0, 0.2)'
                    element.style.border = '2px solid var(--accent)'
                    element.style.borderRadius = '8px'

                    setTimeout(() => {
                        element.style.background = ''
                        element.style.border = ''
                    }, 2000)
                }, 100)
                return
            }
        }

        // Иначе прокручиваем к верху страницы
        window.scrollTo({
            top: top,
            left: left,
            behavior: behavior
        })
    }, [pathname, hash, behavior, top, left])

    return null
}