/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-69gs.js
 * @description Strict mode - checking access to strict function caller from non-strict function (strict function declaration called by Function.prototype.call(globalObject))
 * @noStrict
 * @negative TypeError
 */


function f() { "use strict"; return gNonStrict();};
f.call(fnGlobalObject());


function gNonStrict() {
    return gNonStrict.caller || gNonStrict.caller.throwTypeError;
}

