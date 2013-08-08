/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-8gs.js
 * @description Strict mode - checking access to strict function caller from non-strict function (Function constructor includes strict directive prologue)
 * @noStrict
 * @negative TypeError
 */


var f = Function("\"use strict\";\nreturn gNonStrict();");
f();


function gNonStrict() {
    return gNonStrict.caller || gNonStrict.caller.throwTypeError;
}

