/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-55gs.js
 * @description Strict mode - checking access to strict function caller from non-strict function (Injected setter includes strict directive prologue)
 * @noStrict
 * @negative TypeError
 */


var o = {};
Object.defineProperty(o, "foo", { set: function(stuff) { "use strict"; return gNonStrict(); } });
o.foo = 10;


function gNonStrict() {
    return gNonStrict.caller || gNonStrict.caller.throwTypeError;
}

