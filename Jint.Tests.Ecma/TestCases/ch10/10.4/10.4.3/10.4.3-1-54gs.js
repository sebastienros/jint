/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-54gs.js
 * @description Strict - checking 'this' from a global scope (Literal getter defined within strict mode)
 * @onlyStrict
 */

"use strict";
var o = { get foo() { return this; } }
if (o.foo!==o) {
    throw "'this' had incorrect value!";
}