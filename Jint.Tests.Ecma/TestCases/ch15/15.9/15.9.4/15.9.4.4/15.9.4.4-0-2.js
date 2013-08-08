/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.4/15.9.4.4/15.9.4.4-0-2.js
 * @description Date.now must exist as a function taking 0 parameters
 */


function testcase() {
        return Date.now.length === 0;
    }
runTestCase(testcase);
