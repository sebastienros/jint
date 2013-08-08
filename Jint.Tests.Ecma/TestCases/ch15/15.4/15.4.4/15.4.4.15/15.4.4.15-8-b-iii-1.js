/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-iii-1.js
 * @description Array.prototype.lastIndexOf returns index of last one when more than two elements in array are eligible
 */


function testcase() {

        return [2, 1, 2, 2, 1].lastIndexOf(2) === 3;
    }
runTestCase(testcase);
