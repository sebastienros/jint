/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-2gs.js
 * @description Strict Mode - arguments.callee cannot be accessed in a strict function
 * @onlyStrict
 * @negative .
 */

"use strict";
function f_10_6_1_gs(){
    return arguments.callee;
}
f_10_6_1_gs();

