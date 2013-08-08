/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-50gs.js
 * @description Strict mode - checking access to strict function caller from strict function (Literal setter defined within strict mode)
 * @onlyStrict
 * @negative TypeError
 */


"use strict";
var o = { set foo(stuff) { return gNonStrict(); } }
o.foo = 7; 


function gNonStrict() {
    return gNonStrict.caller;
}

