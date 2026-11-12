import { ClosableServer, drainThenExit, shutdownDelay } from './shutdown';

describe('shutdown', () => {
  it('waits SHUTDOWN_DELAY_MS, 10 s by default', () => {
    expect(shutdownDelay(undefined)).toBe(10_000);
    expect(shutdownDelay('2500')).toBe(2500);
    expect(shutdownDelay('0')).toBe(0);
    expect(shutdownDelay('-5')).toBe(0);
    expect(shutdownDelay('soon')).toBe(0);
  });

  describe('after SIGTERM', () => {
    let scheduled: { run: () => void; ms: number } | undefined;
    let closed: ((error?: Error) => void) | undefined;
    let closeCalls: number;
    const server: ClosableServer = { close: cb => { closeCalls++; closed = cb; return server; } };
    const exit = jasmine.createSpy('exit');
    const flush = jasmine.createSpy('flush').and.callFake(() => Promise.resolve());
    const settle = async () => { for (let i = 0; i < 5; i++) await Promise.resolve(); };

    beforeEach(() => {
      scheduled = undefined;
      closed = undefined;
      closeCalls = 0;
      exit.calls.reset();
      flush.calls.reset();
      drainThenExit(server, flush, exit, 10_000, (run, ms) => scheduled = { run, ms });
    });

    it('keeps serving until the delay is over', () => {
      expect(scheduled?.ms).toBe(10_000);
      expect(closeCalls).toBe(0);
      expect(exit).not.toHaveBeenCalled();
    });

    it('exits only once the requests still open have finished', async () => {
      scheduled!.run();
      await settle();

      expect(closeCalls).toBe(1);
      expect(exit).not.toHaveBeenCalled();

      closed?.();
      await settle();

      expect(flush).toHaveBeenCalled();
      expect(exit).toHaveBeenCalledOnceWith(0);
    });
  });
});
