/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-96gs.js
 * @description Strict mode - checking access to strict function caller from non-strict property (FunctionDeclaration includes strict directive prologue)
 * @noStrict
 * @negative TypeError
 */

var o = { 
    get gNonStrict() {
        var tmp = Object.getOwnPropertyDescriptor(o, "gNonStrict").get;
        return tmp.caller || tmp.caller.throwTypeError;
    }
};


function f() {
    "use strict";
    return o.gNonStrict;
}
f();
