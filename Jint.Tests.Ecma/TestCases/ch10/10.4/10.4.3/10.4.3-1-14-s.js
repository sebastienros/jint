/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-14-s.js
 * @description Strict Mode - checking 'this' (Function constructor includes strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
var f = Function("\"use strict\";\nreturn typeof this;");
return f() === "undefined";
}
runTestCase(testcase);