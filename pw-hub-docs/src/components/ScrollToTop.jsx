import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

export default function ScrollToTop({
                                        behavior = 'smooth',
                                        top = 0,
                                        left = 0
                                    }) {
    const { pathname, hash } = useLocation()

    useEffect(() => {
        // Если есть хэш и мы на главной странице, прокручиваем к элементу
        if (hash && pathname === '/') {
            const id = hash.replace('#', '')
            const element = document.getElementById(id)

            if (element) {
                // Задержка для гарантии, что DOM обновлен
                setTimeout(() => {
                    element.scrollIntoView({
                        behavior: behavior,
                        block: 'start'
                    })
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