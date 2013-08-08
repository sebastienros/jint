/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.4/15.9.4.4/15.9.4.4-0-1.js
 * @description Date.now must exist as a function
 */


function testcase() {
        return typeof Date.now === "function";
    }
runTestCase(testcase);
