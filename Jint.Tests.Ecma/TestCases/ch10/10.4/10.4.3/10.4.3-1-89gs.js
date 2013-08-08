/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-89gs.js
 * @description Strict - checking 'this' from a global scope (non-strict function declaration called by strict Function.prototype.apply(globalObject))
 * @onlyStrict
 */

function f() { return this;};
if ((function () {"use strict"; return f.apply(fnGlobalObject());})() !== fnGlobalObject()){
    throw "'this' had incorrect value!";
}