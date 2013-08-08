/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-26-s.js
 * @description Strict Mode - checking 'this' (New'ed object from Anonymous FunctionExpression includes strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
var obj = new (function () {
    "use strict";
    return this;
});
return (obj !== fnGlobalObject()) && ((typeof obj) !== "undefined");
}
runTestCase(testcase);