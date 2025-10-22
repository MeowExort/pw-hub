// src/components/HowItWorks.jsx
import { motion } from 'framer-motion';
import { useInView } from 'framer-motion';
import { useRef } from 'react';

export default function HowItWorks() {
    const ref = useRef(null);
    const isInView = useInView(ref, { once: true, threshold: 0.3 });

    const steps = [
        {
            number: '01',
            title: 'Создайте отряд',
            description: 'Дайте название вашему отряду аккаунтов для удобства организации',
            icon: '👥'
        },
        {
            number: '02',
            title: 'Добавьте аккаунты',
            description: 'Укажите названия для аккаунтов (логины и пароли НЕ хранятся)',
            icon: '➕'
        },
        {
            number: '03',
            title: 'Авторизуйтесь',
            description: 'Войдите в каждый аккаунт через встроенный браузер напрямую на сайте Perfect World',
            icon: '🔑'
        },
        {
            number: '04',
            title: 'Настройте отряды',
            description: 'Повторите процесс для всех нужных групп аккаунтов',
            icon: '⚙️'
        },
        {
            number: '05',
            title: 'Пользуйтесь!',
            description: 'Запускайте автоматические модули и управляйте всеми аккаунтами сразу',
            icon: '🚀'
        }
    ];

    return (
        <section id="how-it-works" className="py-20 bg-gradient-to-br from-[#0d1430] to-[#1a237e]">
            <div className="max-w-7xl mx-auto px-4">
                <motion.div
                    ref={ref}
                    initial={{ opacity: 0, y: 30 }}
                    animate={isInView ? { opacity: 1, y: 0 } : { opacity: 0, y: 30 }}
                    transition={{ duration: 0.8 }}
                    className="text-center mb-16"
                >
                    <h2 className="text-4xl md:text-5xl font-heading font-bold text-white mb-4">
                        Как <span className="text-[#ffb300]">это работает</span>
                    </h2>
                    <p className="text-xl text-gray-300 font-body max-w-3xl mx-auto">
                        Всего 5 простых шагов отделяют вас от комфортной игры на множестве аккаунтов
                    </p>
                </motion.div>

                <div className="relative">
                    {/* Линия прогресса */}
                    <div className="hidden lg:block absolute left-1/2 top-0 bottom-0 w-1 bg-[#ffb300]/30 transform -translate-x-1/2"></div>

                    <div className="space-y-12 lg:space-y-0">
                        {steps.map((step, index) => (
                            <motion.div
                                key={index}
                                initial={{ opacity: 0, y: 50 }}
                                animate={isInView ? { opacity: 1, y: 0 } : { opacity: 0, y: 50 }}
                                transition={{ duration: 0.6, delay: index * 0.2 }}
                                className={`flex flex-col lg:flex-row items-center gap-8 ${
                                    index % 2 === 0 ? 'lg:flex-row' : 'lg:flex-row-reverse'
                                }`}
                            >
                                {/* Текстовая часть */}
                                <div className={`lg:w-1/2 ${index % 2 === 0 ? 'lg:pr-12' : 'lg:pl-12'}`}>
                                    <div className="bg-gray-800/50 backdrop-blur-sm rounded-2xl p-8 border border-[#ffb300]/20">
                                        <div className="flex items-center gap-4 mb-4">
                                            <div className="text-3xl">{step.icon}</div>
                                            <div className="text-3xl font-heading font-bold text-[#ffb300]">
                                                {step.number}
                                            </div>
                                        </div>
                                        <h3 className="text-2xl font-heading font-bold text-white mb-4">
                                            {step.title}
                                        </h3>
                                        <p className="text-gray-300 font-body leading-relaxed">
                                            {step.description}
                                        </p>
                                    </div>
                                </div>

                                {/* Визуальный разделитель */}
                                <div className="flex-shrink-0">
                                    <div className="w-16 h-16 bg-[#ffb300] rounded-full flex items-center justify-center border-4 border-gray-900 z-10 relative">
                    <span className="text-gray-900 font-heading font-bold text-lg">
                      {index + 1}
                    </span>
                                    </div>
                                </div>

                                {/* Пустой блок для чередования */}
                                <div className="lg:w-1/2"></div>
                            </motion.div>
                        ))}
                    </div>
                </div>

                {/* Финальный призыв к действию */}
                <motion.div
                    initial={{ opacity: 0, y: 30 }}
                    animate={isInView ? { opacity: 1, y: 0 } : { opacity: 0, y: 30 }}
                    transition={{ duration: 0.8, delay: 1.2 }}
                    className="text-center mt-16"
                >
                    <div className="bg-gray-800/50 backdrop-blur-sm rounded-2xl p-8 border border-[#ffb300]/20 max-w-2xl mx-auto">
                        <h3 className="text-3xl font-heading font-bold text-white mb-4">
                            Готовы начать?
                        </h3>
                        <p className="text-gray-300 font-body mb-6 text-lg">
                            Присоединяйтесь к тысячам игроков, которые уже экономят время с нашим лаунчером
                        </p>
                        <motion.button
                            whileHover={{ scale: 1.05 }}
                            whileTap={{ scale: 0.95 }}
                            className="bg-[#ffb300] text-gray-900 px-8 py-4 rounded-lg font-heading font-bold text-lg hover:bg-[#ffc107] transition-colors shadow-lg"
                        >
                            ⬇️ Скачать бесплатно
                        </motion.button>
                        <p className="text-gray-400 font-body text-sm mt-4">
                            Бесплатно • Без регистрации • Без вирусов
                        </p>
                    </div>
                </motion.div>
            </div>
        </section>
    );
}