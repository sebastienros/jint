/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-1gs.js
 * @description Strict Mode - arguments.callee cannot be accessed in a strict function, but does not throw an early error
 * @onlyStrict
 */

"use strict";
function f_10_6_1_gs(){
    return arguments.callee;
}

