import { CLOSE_CAP_MS, ClosableServer, drainThenExit, shutdownDelay } from './shutdown';

describe('shutdown', () => {
  it('waits SHUTDOWN_DELAY_MS, 10 s by default', () => {
    expect(shutdownDelay(undefined)).toBe(10_000);
    expect(shutdownDelay('2500')).toBe(2500);
    expect(shutdownDelay('0')).toBe(0);
    expect(shutdownDelay('-5')).toBe(0);
    expect(shutdownDelay('soon')).toBe(0);
  });

  describe('after SIGTERM', () => {
    let scheduled: { run: () => void; ms: number }[];
    let closed: ((error?: Error) => void) | undefined;
    let closeCalls: number;
    const server: ClosableServer = { close: cb => { closeCalls++; closed = cb; return server; } };
    const exit = jasmine.createSpy('exit');
    const flush = jasmine.createSpy('flush').and.callFake(() => Promise.resolve());
    const settle = async () => { for (let i = 0; i < 5; i++) await Promise.resolve(); };

    beforeEach(() => {
      scheduled = [];
      closed = undefined;
      closeCalls = 0;
      exit.calls.reset();
      flush.calls.reset();
      // A test may make it hang: each starts with one that sends at once
      flush.and.callFake(() => Promise.resolve());
      drainThenExit(server, flush, exit, 10_000, (run, ms) => scheduled.push({ run, ms }));
    });

    it('keeps serving until the delay is over', () => {
      expect(scheduled.map(t => t.ms)).toEqual([10_000]);
      expect(closeCalls).toBe(0);
      expect(exit).not.toHaveBeenCalled();
    });

    it('exits only once the requests still open have finished', async () => {
      scheduled[0].run();
      await settle();

      expect(closeCalls).toBe(1);
      expect(exit).not.toHaveBeenCalled();

      closed?.();
      await settle();

      expect(flush).toHaveBeenCalled();
      expect(exit).toHaveBeenCalledOnceWith(0);
    });

    it('exits even when sending the telemetry hangs, inside Kubernetes\' 30 s', async () => {
      flush.and.callFake(() => new Promise(() => undefined));
      scheduled[0].run();
      scheduled[1].run();
      await settle();

      expect(flush).toHaveBeenCalled();
      expect(scheduled[2]).withContext('a cap on the flush').toBeDefined();
      expect(scheduled[0].ms + scheduled[1].ms + scheduled[2].ms).toBeLessThanOrEqual(28_000);

      scheduled[2].run();
      await settle();

      expect(exit).toHaveBeenCalledOnceWith(0);
    });

    it(`exits after ${CLOSE_CAP_MS} ms when a request never finishes (an open WebSocket), and only once`, async () => {
      scheduled[0].run();
      expect(scheduled[1].ms).toBe(CLOSE_CAP_MS);

      scheduled[1].run();
      await settle();
      closed?.();
      await settle();

      expect(flush).toHaveBeenCalledTimes(1);
      expect(exit).toHaveBeenCalledOnceWith(0);
    });
  });
});
