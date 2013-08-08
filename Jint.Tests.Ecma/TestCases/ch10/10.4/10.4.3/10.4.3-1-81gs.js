/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-81gs.js
 * @description Strict - checking 'this' from a global scope (non-strict function declaration called by strict function declaration)
 * @noStrict
 */

function f() { return this!==undefined;};
function foo() { "use strict"; return f();}
if (! foo()){
    throw "'this' had incorrect value!";
}