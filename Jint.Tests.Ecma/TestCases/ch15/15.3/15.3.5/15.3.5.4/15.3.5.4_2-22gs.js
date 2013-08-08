/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-22gs.js
 * @description Strict mode - checking access to strict function caller from strict function (FunctionExpression defined within a FunctionDeclaration inside strict mode)
 * @onlyStrict
 * @negative TypeError
 */


"use strict";
function f1() {
    var f = function () {
        return gNonStrict();
    }
    return f();
}
f1();


function gNonStrict() {
    return gNonStrict.caller;
}

