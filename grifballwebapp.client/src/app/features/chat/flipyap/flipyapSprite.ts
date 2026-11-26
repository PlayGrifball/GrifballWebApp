/** One filled pixel of a sprite: its column and row, and its colour. */
export interface SpritePixel {
  readonly x: number;
  readonly y: number;
  readonly fill: string;
}

export type FlipyapFrame = 'idle' | 'blink' | 'talk' | 'held';

/** 16 by 16: in the 64px pet, every pixel is 4 screen pixels, so none blur. */
export const SPRITE_SIZE = 16;

/** Where the drawing's top left sits in the grid (it is 14 by 16). */
const OFFSET_X = 1;

/** The colours of the sprite's letters in its drawing below. */
const COLOURS: Readonly<Record<string, string>> = {
  B: '#7a5a08', // methane tank, dark edge
  c: '#e9dccd', // methane tank, cream
  W: '#fcfcfc', // highlight, and the whites of his eyes
  y: '#fff08a', // gold, palest
  Y: '#f8cf38', // gold, lit
  a: '#e6a80f', // gold
  d: '#c98a00', // gold, shaded
  K: '#4a3131', // brown, in the shadows
  D: '#3e3e42', // dark grey: his outline
  M: '#59595d', // grey, shaded
  g: '#7a7a7e', // grey
  L: '#bdbdc0', // grey, lit (the rebreather)
  k: '#222226', // near black (the vents)
  r: '#c4122a', // eyes
  v: '#8fd0ff', // the vents, as he talks
};

/** Flipyap, an Unggoy, top to bottom: the methane tank tilted back, eyes over the rebreather, the gold harness, stubby legs. */
const IDLE: readonly string[] = [
  '....BB........',
  '...BcWB.......',
  '..BycWcB......',
  '..BYWWccB.....',
  '.BYYcDDDDD....',
  '.BYaDMWgMWD...',
  '.KadDMrMMrMD..',
  '.KdDgLLLLLgD..',
  '..KDLgggggLD..',
  '.DgDDkDkDkD...',
  'DgMKdYYYYdKMD.',
  'DMgDdaaaadDgMD',
  '.DD.DMMMMMD.DD',
  '...DgMMDMMgD..',
  '..DgMD...DMgD.',
  '..DDDD...DDDD.',
];

/** The rows of a frame that differ from his resting face (row number, row). */
const FRAME_ROWS: Readonly<Record<FlipyapFrame, Readonly<Record<number, string>>>> = {
  idle: {},
  blink: { 5: '.BYaDMMgMMD...', 6: '.KadDMkMMkMD..' },
  talk: { 9: '.DgDDvvvvvD...' },
  held: { 5: '.BYaDMrgMrD...', 6: '.KadDMWMMWMD..' },
};

function pixels(frame: FlipyapFrame): SpritePixel[] {
  const changes = FRAME_ROWS[frame];
  return IDLE.flatMap((idle, y) =>
    [...(changes[y] ?? idle)].flatMap((letter, column) =>
      letter === '.' ? [] : [{ x: column + OFFSET_X, y, fill: COLOURS[letter] }]));
}

/** Every frame's filled pixels, drawn once (a sprite is small: 16 by 16). */
export const FRAMES: Readonly<Record<FlipyapFrame, readonly SpritePixel[]>> = {
  idle: pixels('idle'),
  blink: pixels('blink'),
  talk: pixels('talk'),
  held: pixels('held'),
};
