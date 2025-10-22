// src/components/Benefits.jsx
import { motion } from 'framer-motion';
import { useInView } from 'framer-motion';
import { useRef, useEffect, useState } from 'react';

export default function Benefits() {
    const ref = useRef(null);
    const isInView = useInView(ref, { once: true, threshold: 0.3 });

    const [stats, setStats] = useState({ activeUsers: null, moduleRuns: null });
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        let isMounted = true;
        const controller = new AbortController();

        async function load() {
            try {
                setLoading(true);
                setError(null);
                const res = await fetch('/api/app/stats', { signal: controller.signal });
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                const data = await res.json();
                if (isMounted) {
                    setStats({
                        activeUsers: typeof data.activeUsers === 'number' ? data.activeUsers : null,
                        moduleRuns: typeof data.moduleRuns === 'number' ? data.moduleRuns : null,
                    });
                }
            } catch (e) {
                if (isMounted && e.name !== 'AbortError') {
                    setError(e);
                }
            } finally {
                if (isMounted) setLoading(false);
            }
        }

        // Загружаем только на клиенте после монтирования
        load();
        return () => {
            isMounted = false;
            controller.abort();
        };
    }, []);

    const benefits = [
        {
            icon: '⚡',
            title: 'Экономия времени',
            description: 'Переключайтесь между аккаунтами в один клик — больше не нужно постоянно вводить пароли и проходить авторизацию',
            stats: 'Экономит до 90% времени'
        },
        {
            icon: '🤖',
            title: 'Автоматизация',
            description: 'Автоматические модули сами активируют промокоды, откроют сундуки караванщика и соберут все бесплатные награды',
            stats: 'Автоматизация 10+ задач'
        },
        {
            icon: '🎁',
            title: 'Удобство переводов',
            description: 'Умные фильтры и снятые ограничения делают перевод предметов из личного кабинета в игру простыми и быстрыми',
            stats: 'Без ограничений'
        }
    ];

    const formatCompact = (n) => {
        if (n == null || isNaN(n)) return '—';
        try {
            return new Intl.NumberFormat('ru-RU', { notation: 'compact', maximumFractionDigits: 1 }).format(n);
        } catch {
            // Fallback, just group thousands
            return n.toLocaleString('ru-RU');
        }
    };

    return (
        <section id="benefits" className="py-20 bg-gradient-to-br from-[#0d1430] to-[#1a237e]">
            <div className="max-w-7xl mx-auto px-4">
                <motion.div
                    ref={ref}
                    initial={{ opacity: 0, y: 30 }}
                    animate={isInView ? { opacity: 1, y: 0 } : { opacity: 0, y: 30 }}
                    transition={{ duration: 0.8 }}
                    className="text-center mb-16"
                >
                    <h2 className="text-4xl md:text-5xl font-heading font-bold text-white mb-4">
                        Почему выбирают <span className="text-[#ffb300]">наше решение</span>
                    </h2>
                    <p className="text-xl text-gray-300 font-body max-w-3xl mx-auto">
                        Все что нужно для комфортной игры на нескольких аккаунтах — в одном приложении
                    </p>
                </motion.div>

                <div className="grid md:grid-cols-3 gap-8">
                    {benefits.map((benefit, index) => (
                        <motion.div
                            key={index}
                            initial={{ opacity: 0, y: 50 }}
                            animate={isInView ? { opacity: 1, y: 0 } : { opacity: 0, y: 50 }}
                            transition={{ duration: 0.6, delay: index * 0.2 }}
                            className="bg-gray-800/50 backdrop-blur-sm rounded-2xl p-8 border border-[#ffb300]/20 hover:border-[#ffb300]/40 transition-all duration-300 group"
                        >
                            <div className="text-4xl mb-4 group-hover:scale-110 transition-transform duration-300">
                                {benefit.icon}
                            </div>
                            <h3 className="text-2xl font-heading font-bold text-white mb-4">
                                {benefit.title}
                            </h3>
                            <p className="text-gray-300 font-body mb-6 leading-relaxed">
                                {benefit.description}
                            </p>
                            <div className="text-[#ffb300] font-heading font-bold text-lg">
                                {benefit.stats}
                            </div>
                        </motion.div>
                    ))}
                </div>

                {/* Дополнительная статистика */}
                <motion.div
                    initial={{ opacity: 0, y: 30 }}
                    animate={isInView ? { opacity: 1, y: 0 } : { opacity: 0, y: 30 }}
                    transition={{ duration: 0.8, delay: 0.6 }}
                    className="mt-16 grid grid-cols-2 md:grid-cols-4 gap-8 text-center"
                >
                    <div>
                        <div className="text-3xl font-heading font-bold text-[#ffb300]">
                            {loading && '…'}
                            {!loading && formatCompact(stats.activeUsers)}
                        </div>
                        <div className="text-gray-400 font-body">активных пользователей</div>
                    </div>
                    <div>
                        <div className="text-3xl font-heading font-bold text-[#ffb300]">
                            {loading && '…'}
                            {!loading && formatCompact(stats.moduleRuns)}
                        </div>
                        <div className="text-gray-400 font-body">выполненных модулей</div>
                    </div>
                    <div>
                        <div className="text-3xl font-heading font-bold text-[#ffb300]">99.9%</div>
                        <div className="text-gray-400 font-body">надежности</div>
                    </div>
                    <div>
                        <div className="text-3xl font-heading font-bold text-[#ffb300]">∞</div>
                        <div className="text-gray-400 font-body">поддержка</div>
                    </div>
                </motion.div>

                {error && (
                    <div className="mt-6 text-center text-sm text-red-300">
                        Не удалось загрузить статистику. Показаны значения по умолчанию.
                    </div>
                )}
            </div>
        </section>
    );
}