/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-56gs.js
 * @description Strict - checking 'this' from a global scope (Literal setter defined within strict mode)
 * @onlyStrict
 */

"use strict";
var x = 2;
var o = { set foo(stuff) { x=this; } }
o.foo = 3;
if (x!==o) {
    throw "'this' had incorrect value!";
}
