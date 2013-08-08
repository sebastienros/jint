/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.5/15.3.5.4/15.3.5.4_2-14gs.js
 * @description Strict mode - checking access to non-strict function caller from non-strict function (indirect eval includes strict directive prologue)
 * @noStrict
 */


var my_eval = eval;
my_eval("\"use strict\";\ngNonStrict();");


function gNonStrict() {
    return gNonStrict.caller;
}

