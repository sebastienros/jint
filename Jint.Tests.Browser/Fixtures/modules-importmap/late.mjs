// Loaded by a dynamic import() from inside main.mjs, after the module graph was already evaluated.
import { bump } from 'counter';

export const arrived = 'late, and the map still applies: ' + bump();
