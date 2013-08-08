/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-51gs.js
 * @description Strict mode - checking access to strict function caller from non-strict function (Literal setter includes strict directive prologue)
 * @noStrict
 * @negative TypeError
 */


var o = { set foo(stuff) { "use strict"; return gNonStrict(); } }
o.foo = 8;


function gNonStrict() {
    return gNonStrict.caller || gNonStrict.caller.throwTypeError;
}

