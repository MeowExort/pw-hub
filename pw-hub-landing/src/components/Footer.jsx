// src/components/Footer.jsx
import { motion } from 'framer-motion';
import { useAppManifest } from '../hooks/useAppManifest';

export default function Footer() {
    const currentYear = new Date().getFullYear();
    const { manifest } = useAppManifest();

    // Функция для плавной прокрутки к секциям
    const scrollToSection = (sectionId) => {
        const element = document.getElementById(sectionId);
        if (element) {
            element.scrollIntoView({ behavior: 'smooth' });
        }
    };

    return (
        <footer className="bg-gray-900 border-t border-gray-800">
            <div className="max-w-7xl mx-auto px-4 py-12">
                <div className="grid md:grid-cols-4 gap-8">
                    {/* Лого и описание */}
                    <div className="md:col-span-2">
                        <div className="flex items-center gap-4 mb-4">
                            {/* Упрощенный логотип для футера */}
                            <div className="flex items-center gap-3">
                                <div className="w-12 h-12 bg-gradient-to-br from-[#ffb300] to-[#ff8f00] rounded-xl flex items-center justify-center shadow-lg">
                                    <span className="font-heading font-bold text-gray-900 text-lg">PW</span>
                                </div>
                                <div>
                                    <div className="font-heading font-bold text-white text-2xl leading-tight">
                                        PW HUB
                                    </div>
                                    <div className="font-heading text-[#ffb300] text-sm leading-tight">
                                        Perfect World Manager
                                        {manifest && (
                                            <span className="text-white ml-2">v{manifest.version}</span>
                                        )}
                                    </div>
                                </div>
                            </div>
                        </div>
                        <p className="text-gray-400 font-body mb-6 leading-relaxed">
                            Мощный менеджер аккаунтов Perfect World.
                            Автоматизируйте рутину и наслаждайтесь игрой.
                        </p>
                        <div className="flex gap-4 flex-wrap">
                            <div className="bg-[#ffb300] text-gray-900 px-4 py-2 rounded-lg font-heading font-bold">
                                🎮 Для игроков
                            </div>
                            <div className="bg-green-500 text-white px-4 py-2 rounded-lg font-heading font-bold">
                                🔒 Безопасно
                            </div>
                        </div>
                    </div>

                    {/* Навигация */}
                    <div>
                        <h4 className="text-lg font-heading font-bold text-white mb-4">
                            Навигация
                        </h4>
                        <ul className="space-y-2">
                            {[
                                { name: 'Главная', id: 'home' },
                                { name: 'Преимущества', id: 'benefits' },
                                { name: 'Модули', id: 'modules' },
                                { name: 'Безопасность', id: 'security' },
                                { name: 'Как работает', id: 'how-it-works' }
                            ].map((item) => (
                                <li key={item.id}>
                                    <button
                                        onClick={() => scrollToSection(item.id)}
                                        className="text-gray-400 hover:text-[#ffb300] font-body transition-colors cursor-pointer hover:underline"
                                    >
                                        {item.name}
                                    </button>
                                </li>
                            ))}
                        </ul>
                    </div>

                    {/* Контакты и ссылки */}
                    <div>
                        <h4 className="text-lg font-heading font-bold text-white mb-4">
                            Ссылки
                        </h4>
                        <ul className="space-y-3">
                            {/* GitHub */}
                            <li>
                                <a
                                    href="https://github.com/MeowExort/pw-hub"
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    className="flex items-center gap-2 text-gray-400 hover:text-[#ffb300] font-body transition-colors group"
                                >
                                    <svg className="w-5 h-5 group-hover:scale-110 transition-transform" fill="currentColor" viewBox="0 0 24 24">
                                        <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"/>
                                    </svg>
                                    <span>Исходный код</span>
                                </a>
                            </li>

                            {/* Скачать приложение */}
                            <li>
                                <a
                                    href={manifest?.url || '#'}
                                    download
                                    className="flex items-center gap-2 text-gray-400 hover:text-[#ffb300] font-body transition-colors group"
                                >
                                    <svg className="w-5 h-5 group-hover:scale-110 transition-transform" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
                                    </svg>
                                    <span>
                    Скачать приложение
                                        {manifest && (
                                            <span className="text-[#ffb300] ml-1">(v{manifest.version})</span>
                                        )}
                  </span>
                                </a>
                            </li>

                            {/* Telegram */}
                            <li>
                                <a
                                    href="https://t.me/pwhubru"
                                    className="flex items-center gap-2 text-gray-400 hover:text-[#ffb300] font-body transition-colors group"
                                >
                                    <svg className="w-5 h-5 group-hover:scale-110 transition-transform" fill="currentColor" viewBox="0 0 24 24">
                                        <path d="M12 0c-6.627 0-12 5.373-12 12s5.373 12 12 12 12-5.373 12-12-5.373-12-12-12zm5.894 8.221l-1.97 9.28c-.145.658-.537.818-1.084.508l-3-2.21-1.447 1.394c-.14.141-.259.259-.374.261l.213-3.053 5.56-5.022c.24-.213-.054-.334-.373-.121l-6.869 4.326-2.96-.924c-.64-.203-.658-.64.136-.954l11.566-4.458c.538-.196 1.006.128.832.941z"/>
                                    </svg>
                                    <span>Telegram группа</span>
                                </a>
                            </li>

                            {/* Документация */}
                            <li>
                                <a
                                    href="https://docs.pw-hub.ru"
                                    className="flex items-center gap-2 text-gray-400 hover:text-[#ffb300] font-body transition-colors group"
                                >
                                    <svg className="w-5 h-5 group-hover:scale-110 transition-transform" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                                    </svg>
                                    <span>База знаний</span>
                                </a>
                            </li>
                        </ul>
                    </div>
                </div>

                {/* Нижняя часть */}
                <div className="border-t border-gray-800 mt-8 pt-8 flex flex-col md:flex-row justify-between items-center">
                    <div className="text-gray-400 font-body mb-4 md:mb-0">
                        © {currentYear} PW Hub. Неофициальный фанатский проект.
                    </div>
                </div>
            </div>
        </footer>
    );
}