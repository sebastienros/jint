/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-24-s.js
 * @description Strict Mode - checking 'this' (New'ed object from FunctionExpression includes strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
var f = function () {
    "use strict";
    return this;
}
return ( (new f())!==fnGlobalObject()) && (typeof (new f()) !== "undefined");
}
runTestCase(testcase);