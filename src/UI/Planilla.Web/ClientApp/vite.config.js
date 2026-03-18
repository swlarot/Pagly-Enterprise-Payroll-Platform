import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
    const isDesktop = mode === 'desktop'

    return {
        plugins: [react()],
        base: '/', // Base URL desde la raíz
        build: {
            // Build de escritorio → wwwroot de Planilla.Web (misma carpeta que usa Tauri como frontendDist)
            // Build web normal → wwwroot de Planilla.Web
            outDir: '../wwwroot',

            // Asegurarse de que solo se limpie este subdirectorio, y no toda la carpeta wwwroot
            emptyOutDir: true,

            // Configuración para generar nombres de archivo predecibles sin hashes aleatorios
            rollupOptions: {
                output: {
                    entryFileNames: 'app.js',
                    chunkFileNames: '[name].js',
                    assetFileNames: 'app.[ext]'
                }
            }
        },
        // En modo desktop, el env file se carga desde Planilla.Desktop/
        envDir: isDesktop ? '../../Planilla.Desktop' : undefined,
        envPrefix: 'VITE_',
    }
})