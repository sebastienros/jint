/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-74gs.js
 * @description Strict - checking 'this' from a global scope (strict function declaration called by Function.prototype.call(someObject))
 * @onlyStrict
 */

var o = {};
function f() { "use strict"; return this===o;};
if (! f.call(o)){
    throw "'this' had incorrect value!";
}