import { defineConfig } from 'vite'
import tailwindcss from '@tailwindcss/vite'

// https://vitejs.dev/config/
export default defineConfig({
    clearScreen: false,
    plugins: [tailwindcss()],
    server: {
        watch: {
            ignored: [
                "**/*.fs" // Don't watch F# files
            ]
        }
    }
})
