// src/hooks/useAppManifest.js
import { useState, useEffect } from 'react';

export function useAppManifest() {
    const [manifest, setManifest] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        const fetchManifest = async () => {
            try {
                setLoading(true);
                const response = await fetch('https://api.pw-hub.ru/api/app/manifest');

                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                const data = await response.json();
                setManifest(data);
                setError(null);
            } catch (err) {
                console.error('Failed to fetch app manifest:', err);
                setError(err.message);
            } finally {
                setLoading(false);
            }
        };

        fetchManifest();

        // Опционально: обновлять манифест каждые 5 минут
        const interval = setInterval(fetchManifest, 5 * 60 * 1000);
        return () => clearInterval(interval);
    }, []);

    return { manifest, loading, error };
}