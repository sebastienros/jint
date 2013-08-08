/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-6-3.js
 * @description Array.prototype.lastIndexOf returns -1 when 'fromIndex' is length of array - 1
 */


function testcase() {

        return [1, 2, 3].lastIndexOf(3, 1) === -1;
    }
runTestCase(testcase);
