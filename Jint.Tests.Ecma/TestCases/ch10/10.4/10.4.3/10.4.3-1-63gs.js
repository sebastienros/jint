/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-63gs.js
 * @description Strict - checking 'this' from a global scope (strict function declaration called by non-strict eval)
 * @onlyStrict
 */

function f() { "use strict"; return this===undefined;};
if (! eval("f();")){
    throw "'this' had incorrect value!";
}