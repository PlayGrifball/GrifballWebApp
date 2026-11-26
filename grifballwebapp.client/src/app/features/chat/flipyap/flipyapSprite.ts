/** One filled pixel of a sprite: its column and row, and its colour. */
export interface SpritePixel {
  readonly x: number;
  readonly y: number;
  readonly fill: string;
}

export type FlipyapFrame = 'idle' | 'blink' | 'talk' | 'held';

export const SPRITE_SIZE = 20;

/** Where the drawing's top left sits in the grid (the drawing is 14 by 18, in the middle of 20 by 20). */
const OFFSET_X = 3;
const OFFSET_Y = 1;

/** The colours of the sprite's letters in its drawing below. */
const COLOURS: Readonly<Record<string, string>> = {
  B: '#7a5a10', // methane tank, dark edge
  Y: '#f2b01e', // gold
  y: '#ffd84a', // gold, lit
  W: '#f6f0dc', // methane tank, highlight
  c: '#d8c9a4', // methane tank, cream
  K: '#4a2c2c', // dark brown
  g: '#8c8c8c', // grey
  L: '#bcbcbc', // grey, light
  D: '#3a3a3e', // grey, dark
  M: '#5c5c60', // grey, mid
  r: '#c8102e', // eye
  w: '#ffffff', // eye, white
  v: '#8fd0ff', // rebreather vents
};

/** Flipyap, an Unggoy, top to bottom: the methane tank tilted back, two red eyes over the rebreather, the gold belt. */
const IDLE: readonly string[] = [
  '..BB..........',
  '.BYWc.........',
  '.ByWWc........',
  'ByyWWcc.......',
  'ByyYMMMMMMMD..',
  'ByyYMwMMMwMD..',
  'KYyDMrMMMrMD..',
  '.KDLLLLLLLLD..',
  '.gDgggggggMD..',
  '.gDDDDDDDDDD..',
  'gMYYyYYYYYMDg.',
  'gMYYYYYYYYMDgg',
  'gMKKKKKKKKMDgg',
  '.gMDDDDDDDMg..',
  '..MDDD.DDDM...',
  '..MMD...DMM...',
  '..MMM...MMM...',
  '.MMMM...MMMM..',
];

/** The rows of a frame that differ from his resting face (row number, row). */
const FRAME_ROWS: Readonly<Record<FlipyapFrame, Readonly<Record<number, string>>>> = {
  idle: {},
  blink: { 5: 'ByyYMMMMMMMD..', 6: 'KYyDMDMMMDMD..' },
  talk: { 8: '.gDgvvvvvvMg..' },
  held: { 5: 'ByyYMrMMMrMD..' },
};

function pixels(frame: FlipyapFrame): SpritePixel[] {
  const changes = FRAME_ROWS[frame];
  return IDLE.flatMap((idle, row) =>
    [...(changes[row] ?? idle)].flatMap((letter, column) =>
      letter === '.' ? [] : [{ x: column + OFFSET_X, y: row + OFFSET_Y, fill: COLOURS[letter] }]));
}

/** Every frame's filled pixels, drawn once (a sprite is small: 20 by 20). */
export const FRAMES: Readonly<Record<FlipyapFrame, readonly SpritePixel[]>> = {
  idle: pixels('idle'),
  blink: pixels('blink'),
  talk: pixels('talk'),
  held: pixels('held'),
};
