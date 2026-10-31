import { expect, test } from './fixtures';

// Halo Infinite through the Halo gateway, end to end: previews run in the default Gateway mode with the
// previews' own gateway key, so the Infinite Client page's status call (a sysadmin endpoint) makes one real
// Halo read, the match stats the halo_infinite health check fetches, through the gateway.

interface HaloStatus {
  mode: 'Gateway' | 'Direct';
  apiKeyConfigured: boolean;
  reachable: boolean | null;
  accountStatus: string | null;
  apiCode: number | null;
  healthy: boolean;
  message: string;
}

test.describe('Halo Infinite', () => {
  test('reads match stats through the Halo gateway', async ({ admin }) => {
    const status = await admin.get<HaloStatus>('Admin/HaloStatus');
    expect(status.mode, 'previews run in Gateway mode').toBe('Gateway');
    // A preview deployed before the homelab gave previews the gateway key and egress to it cannot reach the
    // gateway at all: nothing to test there yet.
    test.skip(!status.apiKeyConfigured, 'This preview has no HaloGateway:ApiKey (not wired to the Halo gateway yet)');

    expect(status.reachable, status.message).toBe(true);
    expect(status.apiCode, status.message).toBe(200);
  });
});
