/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-42gs.js
 * @description Strict mode - checking access to strict function caller from non-strict function (FunctionDeclaration with a strict directive prologue defined within a FunctionExpression)
 * @noStrict
 * @negative TypeError
 */


var f1 = function () {
    function f() {
        "use strict";
        return gNonStrict();
    }
    return f();
}
f1();


function gNonStrict() {
    return gNonStrict.caller || gNonStrict.caller.throwTypeError;
}

