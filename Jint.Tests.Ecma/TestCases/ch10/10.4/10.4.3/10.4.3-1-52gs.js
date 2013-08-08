/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-52gs.js
 * @description Strict - checking 'this' from a global scope (FunctionExpression with a strict directive prologue defined within an Anonymous FunctionExpression)
 * @noStrict
 */

if (! ((function () {
    var f = function () {
        "use strict";
        return typeof this;
    }
    return (f()==="undefined") && (this===fnGlobalObject());
})())) {
    throw "'this' had incorrect value!";
}