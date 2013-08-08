/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-17.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a string containing a number with leading zeros
 */


function testcase() {

        var obj = { 1: 1, 2: 2, length: "0002.0" };

        return Array.prototype.lastIndexOf.call(obj, 1) === 1 &&
            Array.prototype.lastIndexOf.call(obj, 2) === -1;
    }
runTestCase(testcase);
