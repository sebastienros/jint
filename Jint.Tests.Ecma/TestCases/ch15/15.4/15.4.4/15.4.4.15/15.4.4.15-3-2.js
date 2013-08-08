/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-2.js
 * @description Array.prototype.lastIndexOf return -1 when value of 'length' is a boolean (value is true)
 */


function testcase() {
        var obj = { 0: 0, 1: 1, length: true };
        return Array.prototype.lastIndexOf.call(obj, 0) === 0 &&
            Array.prototype.lastIndexOf.call(obj, 1) === -1;
    }
runTestCase(testcase);
