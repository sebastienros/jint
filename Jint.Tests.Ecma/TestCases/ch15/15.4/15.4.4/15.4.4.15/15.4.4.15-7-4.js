/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-7-4.js
 * @description Array.prototype.lastIndexOf returns -1 when abs('fromIndex') is length of array
 */


function testcase() {

        return [1, 2, 3, 4].lastIndexOf(2, -4) === -1;
    }
runTestCase(testcase);
