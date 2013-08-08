/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-42gs.js
 * @description Strict - checking 'this' from a global scope (FunctionDeclaration defined within an Anonymous FunctionExpression with a strict directive prologue)
 * @onlyStrict
 */

if (! ((function () {
    "use strict";
    function f() {
        return typeof this;
    }
    return (f()==="undefined") && ((typeof this)==="undefined");
})())) {
    throw "'this' had incorrect value!";
}