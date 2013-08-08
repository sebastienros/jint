/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-32gs.js
 * @description Strict - checking 'this' from a global scope (Anonymous FunctionExpression defined within a FunctionExpression inside strict mode)
 * @onlyStrict
 */

"use strict";
var f1 = function () {
    return ((function () {
        return typeof this;
    })()==="undefined") && ((typeof this)==="undefined");
}
if (! f1()) {
    throw "'this' had incorrect value!";
}
