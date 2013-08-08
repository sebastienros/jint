/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-80gs.js
 * @description Strict - checking 'this' from a global scope (strict function declaration called by Function.prototype.bind(globalObject)())
 * @onlyStrict
 */

function f() { "use strict"; return this;};
if (f.bind(fnGlobalObject())() !== fnGlobalObject()){
    throw "'this' had incorrect value!";
}