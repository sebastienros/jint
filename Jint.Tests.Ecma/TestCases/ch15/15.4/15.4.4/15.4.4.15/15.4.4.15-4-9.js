/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-9.js
 * @description Array.prototype.lastIndexOf - 'length' is a number of value 0.1
 */


function testcase() {
        var targetObj = [];
        var obj = { 0: targetObj, 100: targetObj, length: 0.1 };
        return Array.prototype.lastIndexOf.call(obj, targetObj) === -1;
    }
runTestCase(testcase);
