/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-15gs.js
 * @description Strict - checking 'this' from a global scope (New'ed Function constructor defined within strict mode)
 * @onlyStrict
 */

"use strict";
var f = new Function("return typeof this;");
if (f() === "undefined") {
    throw "'this' had incorrect value!";
}
