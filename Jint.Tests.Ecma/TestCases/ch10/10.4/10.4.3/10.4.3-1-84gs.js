/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-84gs.js
 * @description Strict - checking 'this' from a global scope (non-strict function declaration called by strict new'ed Function constructor)
 * @noStrict
 */

function f() { return this!==undefined;};
if (! ((function () {return new Function("\"use strict\";return f();")();})()) ){
    throw "'this' had incorrect value!";
}