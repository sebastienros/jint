/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-21-s.js
 * @description Strict Mode - checking 'this' (New'ed object from FunctionDeclaration defined within strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
function f() {
    return this;
}
return ( (new f())!==fnGlobalObject()) && (typeof (new f()) !== "undefined");
}
runTestCase(testcase);