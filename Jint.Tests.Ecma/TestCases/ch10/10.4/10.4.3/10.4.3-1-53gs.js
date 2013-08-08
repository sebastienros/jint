/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-53gs.js
 * @description Strict - checking 'this' from a global scope (Anonymous FunctionExpression with a strict directive prologue defined within an Anonymous FunctionExpression)
 * @noStrict
 */

if (! ((function () {
    return ((function () {
        "use strict";
        return typeof this;
    })()==="undefined") && (this===fnGlobalObject());
})())) {
    throw "'this' had incorrect value!";
}