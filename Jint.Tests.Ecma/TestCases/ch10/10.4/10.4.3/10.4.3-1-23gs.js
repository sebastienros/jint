/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-23gs.js
 * @description Strict - checking 'this' from a global scope (New'ed object from FunctionExpression defined within strict mode)
 * @onlyStrict
 */

"use strict";
var f = function () {
    return this;
}
if (((new f()) === fnGlobalObject()) || (typeof (new f()) === "undefined")) {
    throw "'this' had incorrect value!";
}
