// src/components/Security.jsx
import { motion } from 'framer-motion';
import { useInView } from 'framer-motion';
import { useRef } from 'react';

export default function Security() {
    const ref = useRef(null);
    const isInView = useInView(ref, { once: true, threshold: 0.3 });

    const securityFeatures = [
        {
            icon: '🔒',
            title: 'Пароли не хранятся',
            description: 'Приложение не сохраняет и не имеет доступа к вашим паролям. Авторизация происходит только через официальный сайт.'
        },
        {
            icon: '🌐',
            title: 'Прямое подключение',
            description: 'Встроенный браузер подключается напрямую к серверам Perfect World, минуя посредников.'
        },
        {
            icon: '🛡️',
            title: 'Защита данных',
            description: 'Все данные хранятся локально на вашем компьютере и не передаются третьим лицам.'
        },
        {
            icon: '⚡',
            title: 'Без рисков',
            description: 'Используются только официальные методы работы с игрой, что исключает риск блокировки.'
        }
    ];

    return (
        <section id="security" className="py-20 bg-gray-900">
            <div className="max-w-7xl mx-auto px-4">
                <div className="grid lg:grid-cols-2 gap-12 items-center">
                    {/* Текстовая часть */}
                    <motion.div
                        ref={ref}
                        initial={{ opacity: 0, x: -50 }}
                        animate={isInView ? { opacity: 1, x: 0 } : { opacity: 0, x: -50 }}
                        transition={{ duration: 0.8 }}
                    >
                        <h2 className="text-4xl md:text-5xl font-heading font-bold text-white mb-6">
                            <span className="text-[#ffb300]">Безопасность</span> прежде всего
                        </h2>
                        <p className="text-xl text-gray-300 font-body mb-8 leading-relaxed">
                            Мы понимаем, как важно сохранить ваши аккаунты в безопасности.
                            Поэтому наше приложение построено на принципах максимальной защиты данных.
                        </p>

                        <div className="space-y-6">
                            {securityFeatures.map((feature, index) => (
                                <motion.div
                                    key={index}
                                    initial={{ opacity: 0, x: -30 }}
                                    animate={isInView ? { opacity: 1, x: 0 } : { opacity: 0, x: -30 }}
                                    transition={{ duration: 0.6, delay: 0.3 + index * 0.1 }}
                                    className="flex items-start gap-4"
                                >
                                    <div className="text-2xl flex-shrink-0">{feature.icon}</div>
                                    <div>
                                        <h3 className="text-xl font-heading font-bold text-white mb-2">
                                            {feature.title}
                                        </h3>
                                        <p className="text-gray-300 font-body">
                                            {feature.description}
                                        </p>
                                    </div>
                                </motion.div>
                            ))}
                        </div>
                    </motion.div>

                    {/* Визуальная часть */}
                    <motion.div
                        initial={{ opacity: 0, x: 50 }}
                        animate={isInView ? { opacity: 1, x: 0 } : { opacity: 0, x: 50 }}
                        transition={{ duration: 0.8, delay: 0.4 }}
                        className="relative"
                    >
                        <div className="bg-gray-800 rounded-2xl p-8 border border-[#ffb300]/20">
                            <div className="text-center mb-8">
                                <div className="text-6xl mb-4">🛡️</div>
                                <h3 className="text-2xl font-heading font-bold text-white">
                                    Уровень защиты
                                </h3>
                            </div>

                            <div className="space-y-4">
                                <div className="flex justify-between items-center">
                                    <span className="text-gray-300 font-body">Хранение паролей</span>
                                    <span className="text-red-400 font-heading font-bold">НЕТ</span>
                                </div>
                                <div className="flex justify-between items-center">
                                    <span className="text-gray-300 font-body">Передача данных</span>
                                    <span className="text-green-400 font-heading font-bold">Только PW</span>
                                </div>
                                <div className="flex justify-between items-center">
                                    <span className="text-gray-300 font-body">Локальное хранение</span>
                                    <span className="text-green-400 font-heading font-bold">ДА</span>
                                </div>
                                <div className="flex justify-between items-center">
                                    <span className="text-gray-300 font-body">Риск блокировки</span>
                                    <span className="text-green-400 font-heading font-bold">МИНИМАЛЬНЫЙ</span>
                                </div>
                            </div>

                            <div className="mt-8 p-4 bg-green-900/20 border border-green-500 rounded-lg">
                                <p className="text-green-400 text-center font-body">
                                    ✅ Одобрено сообществом Perfect World
                                </p>
                            </div>
                        </div>

                        {/* Декоративные элементы */}
                        <div className="absolute -top-4 -right-4 w-20 h-20 bg-[#ffb300] rounded-full opacity-20 blur-xl"></div>
                        <div className="absolute -bottom-4 -left-4 w-24 h-24 bg-green-500 rounded-full opacity-20 blur-xl"></div>
                    </motion.div>
                </div>
            </div>
        </section>
    );
}