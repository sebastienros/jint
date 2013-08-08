/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-95gs.js
 * @description Strict mode - checking access to strict function caller from non-strict, constructor-based function (FunctionDeclaration includes strict directive prologue)
 * @noStrict
 * @negative TypeError
 */

var gNonStrict = Function("return gNonStrict.caller || gNonStrict.caller.throwTypeError;");

function f() {
    "use strict";
    return gNonStrict();
}
f();
