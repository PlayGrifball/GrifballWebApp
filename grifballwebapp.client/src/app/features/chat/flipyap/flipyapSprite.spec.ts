import { FRAMES, SPRITE_SIZE, type FlipyapFrame } from './flipyapSprite';

describe('flipyapSprite', () => {
  const frames: FlipyapFrame[] = ['idle', 'blink', 'talk', 'held'];
  const rows = (frame: FlipyapFrame, y: number) => FRAMES[frame].filter(p => p.y === y).map(p => `${p.x}${p.fill}`).join();

  it('draws each frame inside its grid, with a colour for every pixel', () => {
    for (const frame of frames) {
      expect(FRAMES[frame].length).withContext(frame).toBeGreaterThan(0);
      for (const { x, y, fill } of FRAMES[frame]) {
        expect(x >= 0 && x < SPRITE_SIZE && y >= 0 && y < SPRITE_SIZE).withContext(`${frame} ${x},${y}`).toBeTrue();
        expect(fill).withContext(`${frame} ${x},${y}`).toMatch(/^#[0-9a-f]{6}$/);
      }
    }
  });

  it('blinks with the eyes (rows 6 and 7), talks with the rebreather vents (row 9), and is held with red eyes (row 6): the rest stays', () => {
    const changes = { blink: [6, 7], talk: [9], held: [6] } as const;
    for (let y = 0; y < SPRITE_SIZE; y++) {
      for (const frame of ['blink', 'talk', 'held'] as const)
        expect(rows(frame, y) === rows('idle', y)).withContext(`${frame} row ${y}`).toBe(!(changes[frame] as readonly number[]).includes(y));
    }
  });
});
