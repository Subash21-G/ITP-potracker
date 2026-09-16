import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./.pdf-editor-vitest-setup.ts', './.direct-vitest-setup.ts'],
  },
});
