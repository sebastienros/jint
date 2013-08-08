/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-85gs.js
 * @description Strict - checking 'this' from a global scope (non-strict function declaration called by strict Function.prototype.apply())
 * @noStrict
 */

function f() { return this!==undefined;};
if (! ((function () {"use strict"; return f.apply();})())){
    throw "'this' had incorrect value!";
}
