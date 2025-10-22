// src/components/Hero.jsx
import { motion } from 'framer-motion';

export default function Hero() {
    return (
        <section className="min-h-screen bg-gradient-to-br from-[#0d1430] to-[#1a237e] flex items-center justify-center px-4 py-12">
            <div className="max-w-7xl mx-auto grid lg:grid-cols-2 gap-8 lg:gap-16 items-center">
                {/* Текстовая часть */}
                <motion.div
                    initial={{ opacity: 0, x: -50 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ duration: 0.8 }}
                    className="text-white text-center lg:text-left"
                >
                    {/* Логотип */}
                    <motion.div
                        initial={{ opacity: 0, y: -20 }}
                        animate={{ opacity: 1, y: 0 }}
                        transition={{ duration: 0.8, delay: 0.2 }}
                        className="mb-8 flex justify-center lg:justify-start"
                    >
                        <div className="bg-white/10 backdrop-blur-sm rounded-2xl p-4 border border-[#ffb300]/30">
                            <img
                                src="/images/logo.jpg"
                                alt="Perfect World Launcher"
                                className="h-12 md:h-16 object-contain"
                            />
                        </div>
                    </motion.div>

                    <h1 className="text-4xl md:text-5xl lg:text-6xl font-heading font-bold mb-6 leading-tight">
                        Управляй своими{' '}
                        <span className="text-[#ffb300]">аккаунтами</span>{' '}
                        Perfect World
                    </h1>

                    <p className="text-lg md:text-xl mb-8 text-gray-300 leading-relaxed font-body max-w-2xl">
                        Безопасный лаунчер для игроков, который автоматизирует рутину.
                        Приложение <span className="text-[#ffb300] font-semibold">не хранит ваши пароли</span> —
                        авторизация происходит напрямую через официальный сайт.
                    </p>

                    <div className="flex flex-col sm:flex-row gap-4 justify-center lg:justify-start">
                        <motion.button
                            whileHover={{ scale: 1.05 }}
                            whileTap={{ scale: 0.95 }}
                            className="bg-[#ffb300] text-gray-900 px-8 py-4 rounded-lg font-heading font-bold text-lg hover:bg-[#ffc107] transition-colors shadow-lg"
                        >
                            ⬇️ Скачать бесплатно
                        </motion.button>
                        <motion.button
                            whileHover={{ scale: 1.05 }}
                            whileTap={{ scale: 0.95 }}
                            className="border-2 border-[#ffb300] text-[#ffb300] px-8 py-4 rounded-lg font-heading font-bold text-lg hover:bg-[#ffb300] hover:text-gray-900 transition-colors"
                        >
                            ▶️ Смотреть демо
                        </motion.button>
                    </div>

                    {/* Дополнительная информация */}
                    <div className="mt-12 grid grid-cols-1 sm:grid-cols-3 gap-6 text-center lg:text-left">
                        <div>
                            <div className="text-2xl font-heading font-bold text-[#ffb300]">100%</div>
                            <div className="text-gray-400 font-body">Безопасно</div>
                        </div>
                        <div>
                            <div className="text-2xl font-heading font-bold text-[#ffb300]">∞</div>
                            <div className="text-gray-400 font-body">Аккаунтов</div>
                        </div>
                        <div>
                            <div className="text-2xl font-heading font-bold text-[#ffb300]">0₽</div>
                            <div className="text-gray-400 font-body">Бесплатно</div>
                        </div>
                    </div>
                </motion.div>

                {/* Визуальная часть со скриншотом */}
                <motion.div
                    initial={{ opacity: 0, x: 50 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ duration: 0.8, delay: 0.2 }}
                    className="relative"
                >
                    {/* Основной контейнер для скриншота */}
                    <div className="bg-gray-800 rounded-2xl p-3 shadow-2xl border border-[#ffb300]/20 transform perspective-1000">
                        {/* Браузерная рамка */}
                        <div className="flex items-center gap-2 pb-3 border-b border-gray-700">
                            <div className="flex gap-1.5">
                                <div className="w-3 h-3 rounded-full bg-red-500"></div>
                                <div className="w-3 h-3 rounded-full bg-yellow-500"></div>
                                <div className="w-3 h-3 rounded-full bg-green-500"></div>
                            </div>
                            <div className="flex-1 text-center">
                                <span className="text-gray-400 text-sm font-body">Perfect World Launcher</span>
                            </div>
                        </div>

                        {/* Скриншот */}
                        <div className="rounded-lg overflow-hidden border border-gray-700 shadow-inner">
                            <img
                                src="/images/hero-screenshot.png"
                                alt="Интерфейс Perfect World Launcher"
                                className="w-full h-auto object-cover"
                            />
                        </div>
                    </div>

                    {/* Декоративные элементы */}
                    <motion.div
                        animate={{
                            y: [0, -10, 0],
                            opacity: [0.3, 0.5, 0.3]
                        }}
                        transition={{
                            duration: 3,
                            repeat: Infinity,
                            ease: "easeInOut"
                        }}
                        className="absolute -top-6 -right-6 w-20 h-20 bg-[#ffb300] rounded-full opacity-30 blur-xl"
                    ></motion.div>
                    <motion.div
                        animate={{
                            y: [0, 10, 0],
                            opacity: [0.2, 0.4, 0.2]
                        }}
                        transition={{
                            duration: 4,
                            repeat: Infinity,
                            ease: "easeInOut",
                            delay: 1
                        }}
                        className="absolute -bottom-8 -left-8 w-28 h-28 bg-blue-500 rounded-full opacity-20 blur-xl"
                    ></motion.div>

                    {/* Плавающие элементы */}
                    <motion.div
                        animate={{ y: [0, -20, 0] }}
                        transition={{ duration: 6, repeat: Infinity, ease: "easeInOut" }}
                        className="absolute -top-4 right-20 bg-[#ffb300] text-gray-900 px-3 py-1 rounded-full text-sm font-heading font-bold shadow-lg"
                    >
                        Автоматизация
                    </motion.div>
                    <motion.div
                        animate={{ y: [0, 15, 0] }}
                        transition={{ duration: 5, repeat: Infinity, ease: "easeInOut", delay: 2 }}
                        className="absolute bottom-16 -left-4 bg-blue-500 text-white px-3 py-1 rounded-full text-sm font-heading font-bold shadow-lg"
                    >
                        Безопасность
                    </motion.div>
                </motion.div>
            </div>
        </section>
    );
}