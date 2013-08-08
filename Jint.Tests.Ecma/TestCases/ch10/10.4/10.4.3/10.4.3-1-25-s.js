/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-25-s.js
 * @description Strict Mode - checking 'this' (New'ed object from Anonymous FunctionExpression defined within strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
var obj = new (function () {
    return this;
});
return (obj !== fnGlobalObject()) && ((typeof obj) !== "undefined");
}
runTestCase(testcase);