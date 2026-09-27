/**
 * Draws the pool of faces the seeder hands out to players, and writes it to the backend as
 * an embedded resource.
 *
 * A face is a small JSON object (see `FaceConfig`), so a player can be given one and the
 * same face can be drawn again later — which is why it is stored rather than regenerated.
 * The pool is drawn here, in JavaScript, because faces.js is a JavaScript library: the
 * alternative would be a hand-written face generator in C#, and a face is a thing that
 * ought to come from the library that draws it.
 *
 * The draw is seeded, so running this twice produces the same pool and a diff means the
 * pool really changed rather than the weather.
 *
 *   npm run faces:generate
 */
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { generate } from 'facesjs';

/** Enough faces that a whole edition is not handing the same dozen men around. */
const POOL_SIZE = 400;

/** A squad is mostly men, but a women's footballer in a men's league is a bug, not a face. */
const FEMALE_SHARE = 0.12;

const OUTPUT = resolve(
  dirname(fileURLToPath(import.meta.url)),
  '../../../backend/NinjaEleven.Infrastructure/Resources/faces.json'
);

/** mulberry32: small, fast, and the same sequence every time for the same seed. */
function seededRandom(seed) {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const draw = seededRandom(20260927);
const realRandom = Math.random;
Math.random = draw;

try {
  const faces = [];
  for (let index = 0; index < POOL_SIZE; index += 1) {
    faces.push(generate(undefined, { gender: draw() < FEMALE_SHARE ? 'female' : 'male' }));
  }

  mkdirSync(dirname(OUTPUT), { recursive: true });
  writeFileSync(OUTPUT, `${JSON.stringify(faces)}\n`, 'utf8');

  const bytes = JSON.stringify(faces).length;
  console.log(
    `Wrote ${faces.length} faces (${(bytes / 1024).toFixed(0)} KB) to ${OUTPUT}`
  );
} finally {
  Math.random = realRandom;
}
