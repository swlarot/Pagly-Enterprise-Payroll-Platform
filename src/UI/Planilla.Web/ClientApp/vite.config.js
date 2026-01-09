import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [react()],
    base: '/', // Base URL desde la raíz
    build: {
        // Generar los archivos en la carpeta wwwroot de nuestro proyecto
        outDir: '../wwwroot',

        // Asegurarse de que solo se limpie este subdirectorio, y no toda la carpeta wwwroot
        emptyOutDir: true,

        // Configuración para generar nombres de archivo predecibles sin hashes aleatorios
        rollupOptions: {
            output: {
                entryFileNames: 'app.js',
                assetFileNames: 'app.[ext]'
            }
        }
    }
})