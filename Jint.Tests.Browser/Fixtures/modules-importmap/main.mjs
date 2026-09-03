import { bump, name } from 'counter';
import { label } from 'util/label.mjs';

document.getElementById('counter').textContent = name + ':' + bump() + ':' + bump();
document.getElementById('label').textContent = label('mapped');

// A module script is deferred, so the document is parsed by the time this runs -- and the dynamic import
// below resolves "counter" through the same map, from a module the map itself pointed at.
document.getElementById('order').textContent = document.readyState;

const late = await import('./late.mjs');
document.getElementById('late').textContent = late.arrived;
