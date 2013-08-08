/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-26gs.js
 * @description Strict mode - checking access to strict function caller from strict function (Anonymous FunctionExpression defined within a FunctionExpression inside strict mode)
 * @onlyStrict
 * @negative TypeError
 */


"use strict";
var f1 = function () {
    return (function () {
        return gNonStrict();
    })();
}
f1();


function gNonStrict() {
    return gNonStrict.caller;
}

