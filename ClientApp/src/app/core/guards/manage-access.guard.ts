import { operatorGuard } from './auth.guard';

/**
 * Backward-compatible guard name used by existing routes.
 *
 * Admin and Production users can manage records.
 * Viewer users are redirected to Access Denied.
 */
export const manageAccessGuard = operatorGuard;
