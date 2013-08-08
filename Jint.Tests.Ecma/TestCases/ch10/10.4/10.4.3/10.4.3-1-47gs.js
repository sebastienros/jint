/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-47gs.js
 * @description Strict - checking 'this' from a global scope (Anonymous FunctionExpression with a strict directive prologue defined within a FunctionDeclaration)
 * @noStrict
 */

function f1() {
    return ((function () {
        "use strict";
        return typeof this;
    })()==="undefined") && (this===fnGlobalObject());
}
if (! f1()) {
    throw "'this' had incorrect value!";
}