import { vi } from 'vitest';

// PDF tests do not use the WebGL CAD viewer. Its extensionless ESM imports
// cannot be loaded directly by Node, although the browser bundler handles them.
vi.mock('dxf-viewer', () => ({ DxfViewer: class {} }));
