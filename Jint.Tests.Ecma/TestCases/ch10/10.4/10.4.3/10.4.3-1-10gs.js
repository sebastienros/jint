/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-10gs.js
 * @description Strict - checking 'this' from a global scope (FunctionExpression includes strict directive prologue)
 * @onlyStrict
 */

var f = function () {
    "use strict";
    return typeof this;
}
if (f() !== "undefined") {
    throw "'this' had incorrect value!";
}