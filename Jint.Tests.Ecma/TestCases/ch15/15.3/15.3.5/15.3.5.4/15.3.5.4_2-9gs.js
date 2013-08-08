/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-9gs.js
 * @description Strict mode - checking access to non-strict function caller from strict function (New'ed Function constructor defined within strict mode)
 * @onlyStrict
 * @negative TypeError
 */


"use strict";
var f = new Function("return gNonStrict();");
f();


function gNonStrict() {
    return gNonStrict.caller;
}

