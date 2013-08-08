/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-41gs.js
 * @description Strict - checking 'this' from a global scope (Anonymous FunctionExpression defined within a FunctionExpression with a strict directive prologue)
 * @onlyStrict
 */

var f1 = function () {
    "use strict";
    return ((function () {
        return typeof this;
    })()==="undefined") && ((typeof this)==="undefined");
}
if (! f1()) {
    throw "'this' had incorrect value!";
}