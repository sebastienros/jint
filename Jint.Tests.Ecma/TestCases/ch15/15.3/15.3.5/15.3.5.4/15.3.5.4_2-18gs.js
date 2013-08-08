/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-18gs.js
 * @description Strict mode - checking access to strict function caller from non-strict function (New'ed object from FunctionExpression includes strict directive prologue)
 * @noStrict
 * @negative TypeError
 */


var f = function () {
    "use strict";
    return gNonStrict();
}
new f();


function gNonStrict() {
    return gNonStrict.caller || gNonStrict.caller.throwTypeError;
}

