/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-93gs.js
 * @description Strict - checking 'this' from a global scope (non-strict function declaration called by strict Function.prototype.call(someObject))
 * @onlyStrict
 */

var o = {};
function f() { return this===o;};
if (! ((function () {"use strict"; return f.call(o); })())){
    throw "'this' had incorrect value!";
}
