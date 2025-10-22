// src/components/Footer.jsx
import { motion } from 'framer-motion';

export default function Footer() {
    const currentYear = new Date().getFullYear();

    return (
        <footer className="bg-gray-900 border-t border-gray-800">
            <div className="max-w-7xl mx-auto px-4 py-12">
                <div className="grid md:grid-cols-4 gap-8">
                    {/* Лого и описание */}
                    <div className="md:col-span-2">
                        <div className="flex items-center gap-4 mb-4">
                            <img
                                src="/images/logo.jpg"
                                alt="Perfect World Launcher"
                                className="h-10 object-contain"
                            />
                            <h3 className="text-2xl font-heading font-bold text-white">
                                Perfect World Launcher
                            </h3>
                        </div>
                        <p className="text-gray-400 font-body mb-6 leading-relaxed">
                            Мощный инструмент для управления множеством аккаунтов Perfect World.
                            Автоматизируйте рутину и наслаждайтесь игрой.
                        </p>
                        <div className="flex gap-4">
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
                            {['Главная', 'Преимущества', 'Модули', 'Безопасность', 'Как работает'].map((item) => (
                                <li key={item}>
                                    <a
                                        href={`#${item.toLowerCase().replace(' ', '-')}`}
                                        className="text-gray-400 hover:text-[#ffb300] font-body transition-colors"
                                    >
                                        {item}
                                    </a>
                                </li>
                            ))}
                        </ul>
                    </div>

                    {/* Контакты */}
                    <div>
                        <h4 className="text-lg font-heading font-bold text-white mb-4">
                            Поддержка
                        </h4>
                        <ul className="space-y-2 text-gray-400 font-body">
                            <li>💬 Telegram чат</li>
                            <li>📧 Email поддержка</li>
                            <li>📚 База знаний</li>
                            <li>🔄 Обновления</li>
                        </ul>
                    </div>
                </div>

                {/* Нижняя часть */}
                <div className="border-t border-gray-800 mt-8 pt-8 flex flex-col md:flex-row justify-between items-center">
                    <div className="text-gray-400 font-body mb-4 md:mb-0">
                        © {currentYear} Perfect World Launcher. Неофициальный фанатский проект.
                    </div>
                    <div className="text-gray-400 font-body text-sm">
                        Perfect World является зарегистрированной торговой маркой Beijing Perfect World Technology Co., Ltd.
                    </div>
                </div>
            </div>
        </footer>
    );
}