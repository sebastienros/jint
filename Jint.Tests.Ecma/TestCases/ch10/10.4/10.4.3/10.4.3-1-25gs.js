/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-25gs.js
 * @description Strict - checking 'this' from a global scope (New'ed object from Anonymous FunctionExpression defined within strict mode)
 * @onlyStrict
 */

"use strict";
var obj = new (function () {
    return this;
});
if ((obj === fnGlobalObject()) || (typeof obj === "undefined")) {
    throw "'this' had incorrect value!";
}

