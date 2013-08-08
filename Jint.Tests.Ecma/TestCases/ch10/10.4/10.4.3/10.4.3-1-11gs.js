/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-11gs.js
 * @description Strict - checking 'this' from a global scope (Anonymous FunctionExpression defined within strict mode)
 * @onlyStrict
 */

"use strict";
if ((function () {
    return typeof this;
})() !== "undefined") {
    throw "'this' had incorrect value!";
}

