/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-2.js
 * @description Date.prototype.toISOString must exist as a function taking 0 parameters
 */


function testcase() {
        return Date.prototype.toISOString.length === 0;
    }
runTestCase(testcase);
