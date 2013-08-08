/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-35gs.js
 * @description Strict - checking 'this' from a global scope (Anonymous FunctionExpression defined within an Anonymous FunctionExpression inside strict mode)
 * @onlyStrict
 */

"use strict";
if (! ((function () {
    return ((function () {
        return typeof this;
    })()==="undefined") && ((typeof this)==="undefined");
})())) {
    throw "'this' had incorrect value!";
}