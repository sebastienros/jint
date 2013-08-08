/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-59gs.js
 * @description Strict - checking 'this' from a global scope (Injected getter includes strict directive prologue)
 * @onlyStrict
 */

var o = {};
Object.defineProperty(o, "foo", { get: function() { "use strict"; return this; } });
if (o.foo!==o) {
    throw "'this' had incorrect value!";
}