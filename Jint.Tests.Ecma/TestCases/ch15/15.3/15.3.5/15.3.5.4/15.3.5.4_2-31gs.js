/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-31gs.js
 * @description Strict mode - checking access to strict function caller from non-strict function (FunctionExpression defined within a FunctionDeclaration with a strict directive prologue)
 * @noStrict
 * @negative TypeError
 */


function f1() {
    "use strict";
    var f = function () {
        return gNonStrict();
    }
    return f();
}
f1();


function gNonStrict() {
    return gNonStrict.caller || gNonStrict.caller.throwTypeError;
}

