/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-99gs.js
 * @description Strict - checking 'this' from a global scope (non-strict function declaration called by strict Function.prototype.bind(globalObject)())
 * @onlyStrict
 */

function f() { return this===fnGlobalObject();};
if (! ((function () {"use strict"; return f.bind(fnGlobalObject())();})())){
    throw "'this' had incorrect value!";
}
