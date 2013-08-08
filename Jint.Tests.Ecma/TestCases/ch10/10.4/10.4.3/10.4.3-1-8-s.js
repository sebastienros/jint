/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-8-s.js
 * @description Strict Mode - checking 'this' (FunctionDeclaration includes strict directive prologue)
 * @onlyStrict
 */


function testcase() {
function f() {
    "use strict";
    return typeof this;
}
return f() === "undefined";
}
runTestCase(testcase);
