/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-17-s.js
 * @description Strict Mode - checking 'this' (eval used within strict mode)
 * @onlyStrict
 */
    
function testcase() {
"use strict";
return (eval("typeof this") === "undefined") && (eval("this") !== fnGlobalObject());
}
runTestCase(testcase);