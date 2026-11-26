import { FRAMES, SPRITE_SIZE, type FlipyapFrame, type SpritePixel } from './flipyapSprite';

describe('flipyapSprite', () => {
  const frames: FlipyapFrame[] = ['idle', 'blink', 'talk'];
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

  it('blinks with the eyes (row 5) and talks with the mouth (row 8): the rest stays', () => {
    for (let y = 0; y < SPRITE_SIZE; y++) {
      expect(rows('blink', y) === rows('idle', y)).withContext(`blink row ${y}`).toBe(y !== 5);
      expect(rows('talk', y) === rows('idle', y)).withContext(`talk row ${y}`).toBe(y !== 8);
    }
  });
});
