/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-62gs.js
 * @description Strict - checking 'this' from a global scope (strict function declaration called by non-strict function declaration)
 * @onlyStrict
 */

function f() { "use strict"; return this;};
function foo() { return f();}
if (foo()!==undefined){
    throw "'this' had incorrect value!";
}