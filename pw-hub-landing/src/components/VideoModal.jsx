// src/components/VideoModal.jsx
import {motion, AnimatePresence} from 'framer-motion';
import {useEffect} from 'react';

export default function VideoModal({isOpen, onClose}) {
    // Закрытие по ESC
    useEffect(() => {
        const handleEsc = (e) => {
            if (e.keyCode === 27) onClose();
        };
        document.addEventListener('keydown', handleEsc);
        return () => document.removeEventListener('keydown', handleEsc);
    }, [onClose]);

    // Блокировка скролла при открытии модалки
    useEffect(() => {
        if (isOpen) {
            document.body.style.overflow = 'hidden';
        } else {
            document.body.style.overflow = 'unset';
        }
        return () => {
            document.body.style.overflow = 'unset';
        };
    }, [isOpen]);

    return (
        <AnimatePresence>
            {isOpen && (
                <motion.div
                    initial={{opacity: 0}}
                    animate={{opacity: 1}}
                    exit={{opacity: 0}}
                    className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm"
                    onClick={onClose}
                >
                    <motion.div
                        initial={{scale: 0.8, opacity: 0}}
                        animate={{scale: 1, opacity: 1}}
                        exit={{scale: 0.8, opacity: 0}}
                        transition={{type: "spring", damping: 20}}
                        className="relative w-full max-w-4xl bg-gray-900 rounded-2xl overflow-hidden border border-[#ffb300]/30 shadow-2xl"
                        onClick={(e) => e.stopPropagation()}
                    >
                        {/* Заголовок */}
                        <div
                            className="flex items-center justify-between p-4 bg-gradient-to-r from-gray-800 to-gray-900 border-b border-[#ffb300]/20">
                            <h3 className="text-xl font-heading font-bold text-white">
                                Демонстрация работы Perfect World Launcher
                            </h3>
                            <button
                                onClick={onClose}
                                className="w-8 h-8 flex items-center justify-center text-gray-400 hover:text-white hover:bg-gray-700 rounded-full transition-colors"
                            >
                                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                                          d="M6 18L18 6M6 6l12 12"/>
                                </svg>
                            </button>
                        </div>

                        {/* Видео контейнер */}
                        <div className="relative aspect-video bg-black">

                            {/* Реальный видео плеер (раскомментируйте когда будет видео) */}

                            <video
                                className="w-full h-full"
                                controls
                                autoPlay
                                poster="/images/video-poster.jpg"
                            >
                                <source src="/videos/demo.mp4" type="video/mp4"/>
                                Ваш браузер не поддерживает видео.
                            </video>
                        </div>

                        {/* Описание под видео */}
                        <div className="p-6 bg-gray-800">
                            <div className="grid md:grid-cols-3 gap-6 text-center">
                                <div>
                                    <div className="text-[#ffb300] text-lg font-heading font-bold">1 мин 32 сек</div>
                                    <div className="text-gray-400 text-sm font-body">Длительность</div>
                                </div>
                                <div>
                                    <div className="text-[#ffb300] text-lg font-heading font-bold">1080p</div>
                                    <div className="text-gray-400 text-sm font-body">Качество</div>
                                </div>
                                <div>
                                    <div className="text-[#ffb300] text-lg font-heading font-bold">Русский</div>
                                    <div className="text-gray-400 text-sm font-body">Язык</div>
                                </div>
                            </div>
                        </div>
                    </motion.div>
                </motion.div>
            )}
        </AnimatePresence>
    );
}