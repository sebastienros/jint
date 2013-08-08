/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-10-s.js
 * @description Strict Mode - checking 'this' (FunctionExpression includes strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
var f = function () {
    "use strict";
    return typeof this;
}
return f() === "undefined";
}
runTestCase(testcase);