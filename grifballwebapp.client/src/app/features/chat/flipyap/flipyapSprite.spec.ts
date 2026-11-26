import { FRAMES, SPRITE_SIZE, type FlipyapFrame, type SpritePixel } from './flipyapSprite';

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

  it('is the same on both sides', () => {
    for (const frame of frames) {
      const at = new Map(FRAMES[frame].map((p: SpritePixel) => [`${p.x},${p.y}`, p.fill]));
      for (const { x, y, fill } of FRAMES[frame])
        expect(at.get(`${SPRITE_SIZE - 1 - x},${y}`)).withContext(`${frame} ${x},${y}`).toBe(fill);
    }
  });

  it('blinks with the eyes (row 16), talks with the rebreather vents (row 18), and is held with wide eyes (row 16): the rest stays', () => {
    const changes = { blink: [16], talk: [18], held: [16] } as const;
    for (let y = 0; y < SPRITE_SIZE; y++) {
      for (const frame of ['blink', 'talk', 'held'] as const)
        expect(rows(frame, y) === rows('idle', y)).withContext(`${frame} row ${y}`).toBe(!(changes[frame] as readonly number[]).includes(y));
    }
  });
});
