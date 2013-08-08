/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-13gs.js
 * @description Strict - checking 'this' from a global scope (Function constructor defined within strict mode)
 * @onlyStrict
 */

"use strict";
var f = Function("return typeof this;");
if (f() === "undefined") {
    throw "'this' had incorrect value!";
}