/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-11gs.js
 * @description Strict mode - checking access to strict function caller from strict function (eval used within strict mode)
 * @onlyStrict
 * @negative TypeError
 */


"use strict";
eval("gNonStrict();");


function gNonStrict() {
    return gNonStrict.caller;
}

