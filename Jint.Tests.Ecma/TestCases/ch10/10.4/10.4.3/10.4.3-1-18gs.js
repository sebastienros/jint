/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-18gs.js
 * @description Strict - checking 'this' from a global scope (eval includes strict directive prologue)
 * @onlyStrict
 */

if (eval("\"use strict\";\nthis") !== fnGlobalObject()) {
    throw "'this' had incorrect value!";
}