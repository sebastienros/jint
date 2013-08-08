/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-51gs.js
 * @description Strict - checking 'this' from a global scope (FunctionDeclaration with a strict directive prologue defined within an Anonymous FunctionExpression)
 * @noStrict
 */

if (! ((function () {
    function f() {
        "use strict";
        return typeof this;
    }
    return (f()==="undefined") && (this===fnGlobalObject());
})())) {
    throw "'this' had incorrect value!";
}