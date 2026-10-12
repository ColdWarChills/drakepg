import { defineConfig } from 'vite';

export default defineConfig({
  // Relative paths so the build works from any subfolder (itch.io serves games from a CDN path).
  base: './',
  build: {
    outDir: 'dist',
    chunkSizeWarningLimit: 2000,
  },
});
