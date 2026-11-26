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
  o: '#2a1a16', // outline
  a: '#e8872c', // armour
  h: '#f6b25e', // armour, lit
  d: '#b25a1c', // armour, shaded
  s: '#9db0c4', // skin
  n: '#7d90a6', // skin, shaded (the brow)
  w: '#ffffff', // eye
  k: '#1b1b2a', // pupil
  m: '#3a3f4b', // rebreather mask
  l: '#5b6372', // rebreather mask, lit
  v: '#8fd0ff', // rebreather vents
  u: '#6b7280', // hose
  t: '#d6cfae', // methane tank
  g: '#8d8668', // methane tank, shaded
  c: '#6b768f', // methane tank, cap
  b: '#4a3328', // boots
};

/** Flipyap's left half, top to bottom: each row's sixteen letters are mirrored to make the right half. */
const HALF: readonly string[] = [
  '................',
  '................',
  '.occco..........',
  '.ottgo..........',
  '.ottgo...ooooooo',
  '.ottgo..osssssss',
  '.ottgo.ossssssss',
  '.ottgo.osnnnnnnn',
  '.ottgo.oswwkssss',
  '.ottgo.oswkkssss',
  '.ottgo.ossssssss',
  '.ottgo.ommllllll',
  '.ottgo.ommmmmmvv',
  '.ottgo.ommmmmmmm',
  '.ottuuuommmmmmvv',
  '.ottgo.ommmmmmmm',
  '.ottgo...ooooooo',
  '.ottgooaaaaaaaaa',
  '.occcooahaaaaaaa',
  '.....oaaaoaaaaaa',
  '.....oaadoaaaaaa',
  '.....oaadoaahhaa',
  '.....oaadoaaaaaa',
  '.....oaadoddaaaa',
  '.....osssoaaaaaa',
  '.....oooooaaaaaa',
  '.........oaaaaaa',
  '.........oaddaaa',
  '.........ooooooo',
  '.........obbbbo.',
  '........ooooooo.',
  '................',
];

/** The rows of a frame that differ from his resting face (row number, left half). */
const FRAME_ROWS: Readonly<Record<FlipyapFrame, Readonly<Record<number, string>>>> = {
  idle: {},
  blink: { 8: '.ottgo.ossssssss', 9: '.ottgo.oskkkssss' },
  talk: { 13: '.ottgo.ommmmmmvv' },
  held: { 8: '.ottgo.oswwwssss', 9: '.ottgo.oswkwssss' },
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
