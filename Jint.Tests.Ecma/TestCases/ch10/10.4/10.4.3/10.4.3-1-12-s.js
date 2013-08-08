/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-12-s.js
 * @description Strict Mode - checking 'this' (Anonymous FunctionExpression includes strict directive prologue)
 * @onlyStrict
 */
    
function testcase() {
return (function () {
    "use strict";
    return typeof this;
})() === "undefined";
}
runTestCase(testcase);