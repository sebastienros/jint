/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-9gs.js
 * @description Strict - checking 'this' from a global scope (FunctionExpression defined within strict mode)
 * @onlyStrict
 */

"use strict";
var f = function () {
    return typeof this;
}
if (f() !== "undefined") {
    throw "'this' had incorrect value!";
}