import { defineConfig } from 'vite';

// Relative asset paths, so the built game runs from any folder (including inside the Xbox app package).
export default defineConfig({ base: './' });
