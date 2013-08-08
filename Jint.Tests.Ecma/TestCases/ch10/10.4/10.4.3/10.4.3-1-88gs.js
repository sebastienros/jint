/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-88gs.js
 * @description Strict - checking 'this' from a global scope (non-strict function declaration called by strict Function.prototype.apply(someObject))
 * @onlyStrict
 */

var o = {};
function f() { return this===o;};
if (! ((function () {"use strict"; return f.apply(o);})())){
    throw "'this' had incorrect value!";
}