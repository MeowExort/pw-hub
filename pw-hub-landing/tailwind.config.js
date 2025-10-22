// tailwind.config.js
/** @type {import('tailwindcss').Config} */
export default {
    content: [
        "./index.html",
        "./src/**/*.{js,ts,jsx,tsx}",
    ],
    theme: {
        extend: {
            fontFamily: {
                'heading': ['Orbitron', 'sans-serif'],
                'body': ['Exo 2', 'sans-serif'],
            },
            colors: {
                primary: '#1a237e',
                accent: '#ffb300',
                dark: '#0d1430',
            },
            backgroundImage: {
                'gradient-dark': 'linear-gradient(135deg, #0d1430 0%, #1a237e 100%)',
            },
            animation: {
                'float': 'float 6s ease-in-out infinite',
            },
            keyframes: {
                float: {
                    '0%, 100%': { transform: 'translateY(0px)' },
                    '50%': { transform: 'translateY(-20px)' },
                }
            }
        },
    },
    plugins: [],
}