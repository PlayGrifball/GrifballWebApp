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
  o: '#25222f', // outline
  c: '#6b7385', // methane tank cone
  C: '#9aa2b8', // methane tank cone, lit ridge
  a: '#f9a21b', // armour
  h: '#ffc85a', // armour, lit
  d: '#d8620e', // armour, shaded
  p: '#a9b0cf', // skin
  n: '#8089ab', // skin, shaded
  w: '#ffffff', // eye
  k: '#15131c', // pupil
  m: '#3d4150', // rebreather mask
  M: '#5d6378', // rebreather mask, lit
  v: '#8fd0ff', // rebreather vents
  e: '#5a6074', // legs
  f: '#3b3f52', // legs, shaded
};

/** Flipyap's left half, top to bottom: each row's sixteen letters are mirrored to make the right half. */
const HALF: readonly string[] = [
  '................',
  '...............o',
  '..............oC',
  '..............oC',
  '.............oaC',
  '.............oaC',
  '............oaCc',
  '............oaCc',
  '...........oaCcc',
  '...........oaCcc',
  '..........oaCccc',
  '..........oaCccc',
  '.........oaCcccc',
  '........oaCccccc',
  '.....ooopppppppp',
  '....oppppppppppp',
  '....opppppwwpppp',
  '....opppppwkpppp',
  '....oppnnppppppp',
  '....oommmmmmmmmm',
  '.....ommmMMmmmmm',
  '.....oommmmmmmmm',
  '...ooooaaaaaaaaa',
  '..oppoaaahaaddaa',
  '..oppoaaaaaaaaaa',
  '..oppoaadaaaaaaa',
  '...opoaaaaaaaaaa',
  '...ooooddddddddd',
  '......oeeeeeeeeo',
  '......oeefffeeeo',
  '....ooeeeeeeeeeo',
  '....oooooooooooo',
];

/** The rows of a frame that differ from his resting face (row number, left half). */
const FRAME_ROWS: Readonly<Record<FlipyapFrame, Readonly<Record<number, string>>>> = {
  idle: {},
  blink: { 16: '....oppppppppppp', 17: '....opppppkkpppp' },
  talk: { 20: '.....ommmvvvvvvv' },
  held: { 16: '....opppwwwwwppp', 17: '....opppwwkwwppp' },
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
