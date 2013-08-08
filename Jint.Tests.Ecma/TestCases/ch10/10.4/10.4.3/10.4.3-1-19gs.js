/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-19gs.js
 * @description Strict - checking 'this' from a global scope (indirect eval used within strict mode)
 * @onlyStrict
 */

"use strict";
var my_eval = eval;
if (my_eval("this") !== fnGlobalObject()) {
    throw "'this' had incorrect value!";
}
