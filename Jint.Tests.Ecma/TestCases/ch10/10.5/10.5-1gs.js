/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.5/10.5-1gs.js
 * @description Strict Mode - arguments cannot be assigned to in a strict function
 * @onlyStrict
 * @negative ^((?!NotEarlyError).)*$
 */

"use strict";
throw NotEarlyError;

function f_10_5_1_gs(){
    arguments = 7;
}

