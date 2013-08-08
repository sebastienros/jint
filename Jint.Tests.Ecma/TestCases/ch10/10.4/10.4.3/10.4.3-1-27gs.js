/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-27gs.js
 * @description Strict - checking 'this' from a global scope (FunctionDeclaration defined within a FunctionDeclaration inside strict mode)
 * @onlyStrict
 */

"use strict";
function f1() {
    function f() {
        return typeof this;
    }
    return (f()==="undefined") && ((typeof this)==="undefined");
}
if (! f1()) {
    throw "'this' had incorrect value!";
}