/** One filled pixel of a sprite: its column and row, and its colour. */
export interface SpritePixel {
  readonly x: number;
  readonly y: number;
  readonly fill: string;
}

export type FlipyapFrame = 'idle' | 'blink' | 'talk' | 'held';

export const SPRITE_SIZE = 16;

/** The colours of the sprite's letters in its drawings below. */
const COLOURS: Readonly<Record<string, string>> = {
  o: '#2a1a16', // outline
  a: '#e8872c', // armour
  d: '#b25a1c', // armour, shaded
  s: '#9db0c4', // face
  w: '#ffffff', // eye
  k: '#1b1b2a', // pupil
  t: '#d6cfae', // methane tank
  g: '#8d8668', // methane tank, shaded
  m: '#5a1f1f', // mouth
  r: '#d9534f', // tongue
};

/** Flipyap's left half, top to bottom: each row's eight letters are mirrored to make the right half. */
const HALF: readonly string[] = [
  '........',
  '.....ooo',
  '....oaaa',
  '...oaadd',
  '...oasss',
  '...oawws',
  '...oakks',
  '...oasmm',
  '...oasss',
  'otgoaaaa',
  'otgoaaaa',
  'otgoaadd',
  '.oooaaaa',
  '....oaao',
  '...oaaao',
  '...ooooo',
];

/** The rows of a frame that differ from his resting face (row number, left half). */
const FRAME_ROWS: Readonly<Record<FlipyapFrame, Readonly<Record<number, string>>>> = {
  idle: {},
  blink: { 5: '...oasss' },
  talk: { 8: '...oassr' },
  held: { 6: '...oawws', 8: '...oasmm' },
};

function pixels(frame: FlipyapFrame): SpritePixel[] {
  const changes = FRAME_ROWS[frame];
  return HALF.flatMap((half, y) => {
    const left = changes[y] ?? half;
    return [...(left + [...left].reverse().join(''))].flatMap((letter, x) =>
      letter === '.' ? [] : [{ x, y, fill: COLOURS[letter] }]);
  });
}

/** Every frame's filled pixels, drawn once (a sprite is small: 16 by 16). */
export const FRAMES: Readonly<Record<FlipyapFrame, readonly SpritePixel[]>> = {
  idle: pixels('idle'),
  blink: pixels('blink'),
  talk: pixels('talk'),
  held: pixels('held'),
};
