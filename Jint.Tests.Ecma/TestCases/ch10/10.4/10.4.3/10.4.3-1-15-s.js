/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-15-s.js
 * @description Strict Mode - checking 'this' (New'ed Function constructor defined within strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
var f = new Function("return typeof this;");
return f() !== "undefined";
}
runTestCase(testcase);