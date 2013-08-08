/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-2-3.js
 * @description Array.isArray applied to an Array-like object with length and some indexed properties
 */


function testcase() {

        return !Array.isArray({ 0: 12, 1: 9, length: 2 });
    }
runTestCase(testcase);
