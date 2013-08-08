/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-3.js
 * @description Date.prototype.toISOString must exist as a function
 */


function testcase() {
        return typeof (Date.prototype.toISOString) === "function";
    }
runTestCase(testcase);
