// Reached through the bare specifier "counter", which only an import map can resolve.
let value = 0;

export function bump() {
  value += 1;
  return value;
}

export const name = 'counter';
