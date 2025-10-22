// src/pages/index.page.jsx
import Hero from '../components/Hero'
import Benefits from '../components/Benefits'
import Modules from '../components/Modules'
import Security from '../components/Security'
import HowItWorks from '../components/HowItWorks'
import Footer from '../components/Footer'

export function Page() {
    return (
        <div className="bg-gray-900">
            <Hero />
            <Benefits />
            <Modules />
            <Security />
            <HowItWorks />
            <Footer />
        </div>
    )
}

export const documentProps = {
    title: 'Perfect World Launcher - Управление аккаунтами',
    description: 'Автоматизируйте управление аккаунтами Perfect World. Безопасно, удобно, бесплатно.'
}