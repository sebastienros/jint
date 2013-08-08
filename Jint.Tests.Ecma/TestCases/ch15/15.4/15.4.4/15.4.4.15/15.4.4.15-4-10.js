/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-10.js
 * @description Array.prototype.lastIndexOf - 'length' is a number of value -6e-1
 */


function testcase() {
        var targetObj = [];
        var obj = { 0: targetObj, 100: targetObj, length: -6e-1 };
        return Array.prototype.lastIndexOf.call(obj, targetObj) === -1;
    }
runTestCase(testcase);
