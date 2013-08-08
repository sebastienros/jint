/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-20gs.js
 * @description Strict - checking 'this' from a global scope (indirect eval includes strict directive prologue)
 * @onlyStrict
 */

var my_eval = eval;
if (my_eval("\"use strict\";\nthis") !== fnGlobalObject() ) {
    throw "'this' had incorrect value!";
}