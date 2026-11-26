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
  g: '#5a5a68', // charcoal armour
  G: '#7e7e8c', // charcoal armour, lit
  e: '#8a8a96', // face
  E: '#a9a9b5', // face, lit
  m: '#2c2c36', // rebreather mask
  M: '#555563', // rebreather mask, lit
  w: '#ffffff', // eye
  k: '#15131c', // pupil
  v: '#8fd0ff', // rebreather vents
};

/** A row of the drawing: <letters> start at column <x> ('.' leaves a gap). */
const at = (x: number, letters: string): string => '.'.repeat(x) + letters;

/** Flipyap facing left, top to bottom: the cone leaning back, the face and rebreather at the front, one arm forward. */
const IDLE: readonly string[] = [
  '',
  '',
  at(20, 'oo'),
  at(19, 'oCao'),
  at(18, 'oCCao'),
  at(17, 'oCCCao'),
  at(16, 'oCCCcao'),
  at(15, 'oCCCccao'),
  at(14, 'oCCCcccao'),
  at(13, 'oCCCccccao'),
  at(12, 'oCCCcccccao'),
  at(11, 'ooggggggggo'),
  at(9, 'ogggggggggggGo'),
  at(8, 'oEEEEeggggggggGo'),
  at(8, 'okwEEegggggggggo'),
  at(7, 'ommmMmegggggggggo'),
  at(7, 'ommkMkmgggggggggo'),
  at(7, 'ommmmmmgggggggggo'),
  at(8, 'ooommmgggggggggo'),
  at(10, 'ooogggggggggo'),
  at(8, 'ooggggggggggggggo'),
  at(6, 'oggGoaaahaaaaggggggo'),
  at(5, 'ogggGoaaaaaaaaggggggo'),
  at(5, 'ogggGoaaadaaaaggggggo'),
  at(5, 'ogGgoaaaaaaaaagggggo'),
  at(6, 'oggodddddddddddddo'),
  at(8, 'ooggggggggggggo'),
  at(8, 'ogGggo..oggggo'),
  at(7, 'oggggo...ogGggo'),
  at(5, 'oggggggo...oggggo'),
  at(5, 'oooooooo...oooooo'),
  '',
];

/** The rows of a frame that differ from his resting face (row number, row). */
const FRAME_ROWS: Readonly<Record<FlipyapFrame, Readonly<Record<number, string>>>> = {
  idle: {},
  blink: { 14: at(8, 'okkEEegggggggggo') },
  talk: { 16: at(7, 'ovvvvvmgggggggggo') },
  held: { 14: at(8, 'owwEEegggggggggo') },
};

function pixels(frame: FlipyapFrame): SpritePixel[] {
  const changes = FRAME_ROWS[frame];
  return IDLE.flatMap((idle, y) =>
    [...(changes[y] ?? idle)].flatMap((letter, x) => (letter === '.' ? [] : [{ x, y, fill: COLOURS[letter] }])));
}

/** Every frame's filled pixels, drawn once (a sprite is small: 32 by 32). */
export const FRAMES: Readonly<Record<FlipyapFrame, readonly SpritePixel[]>> = {
  idle: pixels('idle'),
  blink: pixels('blink'),
  talk: pixels('talk'),
  held: pixels('held'),
};
