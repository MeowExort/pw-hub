// src/components/Modules.jsx
import { motion } from 'framer-motion';
import { useInView } from 'framer-motion';
import { useRef } from 'react';
import { useAppManifest } from '../hooks/useAppManifest';

export default function Modules() {
    const ref = useRef(null);
    const isInView = useInView(ref, { once: true, threshold: 0.3 });
    const { manifest, loading, error } = useAppManifest();

    // Текст для кнопки скачивания в зависимости от состояния
    const getDownloadButtonText = () => {
        if (loading) return '🔄 Загрузка...';
        if (error) return '⬇️ Попробовать модули';
        if (manifest) return `⬇️ Попробовать модули (v${manifest.version})`;
        return '⬇️ Скачать бесплатно';
    };

    // URL для скачивания
    const downloadUrl = manifest?.url || '#';

    return (
        <section id="modules" className="py-20 bg-gray-900">
            <div className="max-w-7xl mx-auto px-4">
                {/* Заголовок секции */}
                <motion.div
                    ref={ref}
                    initial={{ opacity: 0, y: 30 }}
                    animate={isInView ? { opacity: 1, y: 0 } : { opacity: 0, y: 30 }}
                    transition={{ duration: 0.8 }}
                    className="text-center mb-16"
                >
                    <h2 className="text-4xl md:text-5xl font-heading font-bold text-white mb-4">
                        Автоматические <span className="text-[#ffb300]">модули</span>
                    </h2>
                    <p className="text-xl text-gray-300 font-body max-w-3xl mx-auto">
                        Готовые решения для автоматизации рутинных задач. Запускайте модули и наблюдайте за процессом в реальном времени.
                    </p>
                </motion.div>

                <div className="grid lg:grid-cols-2 gap-12 items-center">
                    {/* Левая часть - описание модулей */}
                    <motion.div
                        initial={{ opacity: 0, x: -50 }}
                        animate={isInView ? { opacity: 1, x: 0 } : { opacity: 0, x: -50 }}
                        transition={{ duration: 0.8, delay: 0.2 }}
                        className="space-y-8"
                    >
                        {/* Модуль 1 */}
                        <div className="bg-gray-800 rounded-2xl p-6 border-l-4 border-[#ffb300]">
                            <h3 className="text-2xl font-heading font-bold text-white mb-3">
                                🎁 Сундук Караванщика
                            </h3>
                            <p className="text-gray-300 font-body mb-4">
                                Автоматически находит и активирует сундуки караванщика на всех ваших аккаунтах.
                                Модуль сам переключается между аккаунтами и собирает награды.
                            </p>
                            <ul className="text-gray-400 font-body space-y-2">
                                <li>✅ Автоматическое переключение между аккаунтами</li>
                                <li>✅ Поиск всех доступных сундуков</li>
                                <li>✅ Детальная статистика выполнения</li>
                                <li>✅ Время выполнения: ~20 секунд на 8 аккаунтов</li>
                            </ul>
                        </div>

                        {/* Модуль 2 */}
                        <div className="bg-gray-800 rounded-2xl p-6 border-l-4 border-blue-500">
                            <h3 className="text-2xl font-heading font-bold text-white mb-3">
                                🎫 Активация промокодов
                            </h3>
                            <p className="text-gray-300 font-body mb-4">
                                Вводите промокоды один раз — модуль активирует их на всех ваших аккаунтах автоматически.
                            </p>
                            <ul className="text-gray-400 font-body space-y-2">
                                <li>✅ Массовая активация промокодов</li>
                                <li>✅ Автоматическая проверка действительности</li>
                                <li>✅ Отчет о успешных активациях</li>
                            </ul>
                        </div>

                        {/* Модуль 3 */}
                        <div className="bg-gray-800 rounded-2xl p-6 border-l-4 border-green-500">
                            <h3 className="text-2xl font-heading font-bold text-white mb-3">
                                📦 Перевод предметов
                            </h3>
                            <p className="text-gray-300 font-body mb-4">
                                Умная система переводов с удобными фильтрами и снятыми ограничениями.
                            </p>
                            <ul className="text-gray-400 font-body space-y-2">
                                <li>✅ Умные фильтры выбора предметов</li>
                                <li>✅ Снятие ограничений на количество</li>
                                <li>✅ Безопасные переводы между аккаунтами</li>
                            </ul>
                        </div>
                    </motion.div>

                    {/* Правая часть - скриншот процесса */}
                    <motion.div
                        initial={{ opacity: 0, x: 50 }}
                        animate={isInView ? { opacity: 1, x: 0 } : { opacity: 0, x: 50 }}
                        transition={{ duration: 0.8, delay: 0.4 }}
                        className="relative"
                    >
                        {/* Контейнер для скриншота */}
                        <div className="bg-gray-800 rounded-2xl p-3 shadow-2xl border border-[#ffb300]/20">
                            {/* Заголовок модуля в интерфейсе */}
                            <div className="flex items-center justify-between pb-3 border-b border-gray-700 mb-4">
                                <h4 className="text-lg font-heading font-bold text-[#ffb300]">
                                    Выполнение модуля
                                </h4>
                                <div className="text-sm text-gray-400 font-body">
                                    Модуль — Сундук Караванщика
                                </div>
                            </div>

                            {/* Консоль вывода */}
                            <div className="bg-black rounded-lg p-4 font-mono text-sm text-green-400 h-96 overflow-y-auto">
                                <div className="space-y-3">
                                    <div className="text-blue-400">--- АКТИВАЦИЯ СУНДУКОВ КАРАВАНЩИКА ---</div>
                                    <div>Всего аккаунтов: 8</div>

                                    <div className="ml-4">
                                        <div className="text-yellow-400">[1/8] Копатыч:</div>
                                        <div className="ml-4 text-gray-400">- Копатыч = сундуков не найдено</div>
                                    </div>

                                    <div className="ml-4">
                                        <div className="text-yellow-400">[2/8] Лосяш:</div>
                                        <div className="ml-4 text-gray-400">- Лосяш = сундуков не найдено</div>
                                    </div>

                                    <div className="ml-4">
                                        <div className="text-yellow-400">[3/8] Пин:</div>
                                        <div className="ml-4">
                                            <div className="text-gray-300">Аккаунт 3/8: Пин</div>
                                            {/* Прогресс-бар */}
                                            <div className="w-full bg-gray-700 rounded-full h-2 mt-2">
                                                <motion.div
                                                    initial={{ width: 0 }}
                                                    animate={isInView ? { width: "25%" } : { width: 0 }}
                                                    transition={{ duration: 1, delay: 1 }}
                                                    className="bg-[#ffb300] h-2 rounded-full"
                                                ></motion.div>
                                            </div>
                                            <div className="text-xs text-gray-400 mt-1">25% выполнено</div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Статус бар */}
                            <div className="flex justify-between items-center mt-4 text-sm text-gray-400">
                                <div>🟢 Модуль выполняется</div>
                                <div>⏱️ Время: 0:19</div>
                            </div>
                        </div>

                        {/* Декоративные элементы */}
                        <div className="absolute -top-4 -right-4 w-20 h-20 bg-[#ffb300] rounded-full opacity-20 blur-xl"></div>
                        <div className="absolute -bottom-4 -left-4 w-24 h-24 bg-blue-500 rounded-full opacity-20 blur-xl"></div>
                    </motion.div>
                </div>

                {/* Призыв к действию */}
                <motion.div
                    initial={{ opacity: 0, y: 30 }}
                    animate={isInView ? { opacity: 1, y: 0 } : { opacity: 0, y: 30 }}
                    transition={{ duration: 0.8, delay: 0.6 }}
                    className="text-center mt-16"
                >
                    <p className="text-gray-300 font-body text-lg mb-6">
                        Все модули работают полностью автоматически и не требуют вашего участия
                    </p>

                    <motion.a
                        whileHover={{ scale: loading ? 1 : 1.05 }}
                        whileTap={{ scale: loading ? 1 : 0.95 }}
                        href={downloadUrl}
                        download
                        className={`px-8 py-4 rounded-lg font-heading font-bold text-lg transition-colors shadow-lg ${
                            loading
                                ? 'bg-gray-400 text-gray-700 cursor-not-allowed'
                                : 'bg-[#ffb300] text-gray-900 hover:bg-[#ffc107]'
                        }`}
                        onClick={(e) => {
                            if (loading || error) {
                                e.preventDefault();
                            }
                        }}
                    >
                        {getDownloadButtonText()}
                        {loading && (
                            <motion.div
                                animate={{ rotate: 360 }}
                                transition={{ duration: 1, repeat: Infinity, ease: "linear" }}
                                className="w-5 h-5 border-2 border-gray-700 border-t-gray-900 rounded-full"
                            />
                        )}
                    </motion.a>


                    {/* Статус загрузки */}
                    {error && (
                        <motion.div
                            initial={{ opacity: 0, y: 10 }}
                            animate={{ opacity: 1, y: 0 }}
                            className="mt-4 p-3 bg-red-900/20 border border-red-500 rounded-lg"
                        >
                            <p className="text-red-400 text-sm font-body">
                                Не удалось загрузить информацию о версии. {error}
                            </p>
                        </motion.div>
                    )}
                </motion.div>
            </div>
        </section>
    );
}