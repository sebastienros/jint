/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-16.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' is a string containing Infinity
 */


function testcase() {
        var arr = [];
        arr[Math.pow(2, 32) - 2] = true; // length is the max value of Uint type
        return arr.lastIndexOf(true, "Infinity") === (Math.pow(2, 32) - 2);
    }
runTestCase(testcase);
