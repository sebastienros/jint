/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-46gs.js
 * @description Strict mode - checking access to strict function caller from non-strict function (FunctionExpression with a strict directive prologue defined within an Anonymous FunctionExpression)
 * @noStrict
 * @negative TypeError
 */


(function () {
    var f = function () {
        "use strict";
        return gNonStrict();
    }
    return f();
})();


function gNonStrict() {
    return gNonStrict.caller || gNonStrict.caller.throwTypeError;
}

