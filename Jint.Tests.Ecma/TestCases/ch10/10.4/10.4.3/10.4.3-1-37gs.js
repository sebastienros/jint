/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-37gs.js
 * @description Strict - checking 'this' from a global scope (FunctionExpression defined within a FunctionDeclaration with a strict directive prologue)
 * @onlyStrict
 */

function f1() {
    "use strict";
    var f = function () {
        return typeof this;
    }
    return (f()==="undefined") && ((typeof this)==="undefined");
}
if (! f1()) {
    throw "'this' had incorrect value!";
}