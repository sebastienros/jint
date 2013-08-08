/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-23-s.js
 * @description Strict Mode - checking 'this' (New'ed object from FunctionExpression defined within strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
var f = function () {
    return this;
}
return ( (new f())!==fnGlobalObject()) && (typeof (new f()) !== "undefined");

}
runTestCase(testcase);