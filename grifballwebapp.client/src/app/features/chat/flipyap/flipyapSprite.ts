/** One filled pixel of a sprite: its column and row, and its colour. */
export interface SpritePixel {
  readonly x: number;
  readonly y: number;
  readonly fill: string;
}

export type FlipyapFrame = 'idle' | 'blink' | 'talk' | 'held';

export const SPRITE_SIZE = 32;

/** The colours of the sprite's letters in its drawings below. */
const COLOURS: Readonly<Record<string, string>> = {
  o: '#1a1a22', // outline
  C: '#efe9d2', // methane tank cone
  c: '#bdb59a', // methane tank cone, shaded
  a: '#e6a82a', // gold armour
  d: '#b27818', // gold armour, shaded
  h: '#ffd35c', // gold armour, lit
  g: '#4a4a54', // charcoal armour
  G: '#6d6d79', // charcoal armour, lit
  e: '#8a8a96', // face
  E: '#a9a9b5', // face, lit
  w: '#ffffff', // eye
  k: '#23232b', // pupil and grille
  v: '#8fd0ff', // rebreather vents
};

/** Flipyap's left half, top to bottom: each row's sixteen letters are mirrored to make the right half. */
const HALF: readonly string[] = [
  '................',
  '................',
  '...............o',
  '..............oC',
  '.............oaC',
  '.............oaC',
  '............oaCC',
  '............oaCC',
  '...........oaCCc',
  '...........oaCCc',
  '..........oaCCcc',
  '..........oaCCcc',
  '.........oaCCccc',
  '.......ooggggggg',
  '......oggggggGGG',
  '.....oggeeeeEEEE',
  '.....ogeeewkEEEE',
  '.....ogeeeeEEEEE',
  '.....ogggkgkgkgk',
  '......oggggggggg',
  '....oooggggggggg',
  '...ogggoaaahaaaa',
  '...ogGgoaaaaaaaa',
  '...ogggoaadaaaaa',
  '....ogoaaaaaaaaa',
  '....oooddddddddd',
  '.......ogggggggg',
  '......ogggkggggo',
  '......ogGggggggo',
  '.....ooggggggggo',
  '.....ooooooooooo',
  '................',
];

/** The rows of a frame that differ from his resting face (row number, left half). */
const FRAME_ROWS: Readonly<Record<FlipyapFrame, Readonly<Record<number, string>>>> = {
  idle: {},
  blink: { 16: '.....ogeekkkEEEE' },
  talk: { 18: '.....ogggvvvvvvv' },
  held: { 16: '.....ogeewwwEEEE' },
};

function pixels(frame: FlipyapFrame): SpritePixel[] {
  const changes = FRAME_ROWS[frame];
  return HALF.flatMap((half, y) => {
    const left = changes[y] ?? half;
    return [...(left + [...left].reverse().join(''))].flatMap((letter, x) =>
      letter === '.' ? [] : [{ x, y, fill: COLOURS[letter] }]);
  });
}

/** Every frame's filled pixels, drawn once (a sprite is small: 32 by 32). */
export const FRAMES: Readonly<Record<FlipyapFrame, readonly SpritePixel[]>> = {
  idle: pixels('idle'),
  blink: pixels('blink'),
  talk: pixels('talk'),
  held: pixels('held'),
};
