/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-7gs.js
 * @description Strict mode - checking access to non-strict function caller from strict function (Function constructor defined within strict mode)
 * @onlyStrict
 * @negative TypeError
 */


"use strict";
var f = Function("return gNonStrict();");
f();


function gNonStrict() {
    return gNonStrict.caller;
}

