/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-67gs.js
 * @description Strict - checking 'this' from a global scope (strict function declaration called by Function.prototype.apply(null))
 * @onlyStrict
 */

function f() { "use strict"; return this===null;};
if (! f.apply(null)){
    throw "'this' had incorrect value!";
}
