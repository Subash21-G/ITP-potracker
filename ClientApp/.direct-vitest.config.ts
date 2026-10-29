import {
  defineConfig,
} from 'vitest/config';

export default defineConfig({
  test: {
    // Transform the viewer's extensionless imports through Vite before Node loads them.
    server: {
      deps: {
        inline: ['dxf-viewer'],
      },
    },
    environment: 'jsdom',
    globals: true,
    setupFiles: [
      './.direct-vitest-setup.ts',
    ],
  },
});
