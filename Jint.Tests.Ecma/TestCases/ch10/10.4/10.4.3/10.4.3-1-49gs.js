/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-49gs.js
 * @description Strict - checking 'this' from a global scope (FunctionExpression with a strict directive prologue defined within a FunctionExpression)
 * @noStrict
 */

var f1 = function () {
    var f = function () {
        "use strict";
        return typeof this;
    }
    return (f()==="undefined") && (this===fnGlobalObject());
}
if (! f1()) {
    throw "'this' had incorrect value!";
}