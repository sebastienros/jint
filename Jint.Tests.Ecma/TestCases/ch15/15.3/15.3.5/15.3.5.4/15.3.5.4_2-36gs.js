/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-36gs.js
 * @description Strict mode - checking access to strict function caller from non-strict function (FunctionDeclaration defined within an Anonymous FunctionExpression with a strict directive prologue)
 * @noStrict
 * @negative TypeError
 */


(function () {
    "use strict";
    function f() {
        return gNonStrict();
    }
    return f();
})();


function gNonStrict() {
    return gNonStrict.caller || gNonStrict.caller.throwTypeError;
}

