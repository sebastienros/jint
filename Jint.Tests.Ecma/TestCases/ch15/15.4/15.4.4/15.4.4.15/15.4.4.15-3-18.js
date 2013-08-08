/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-18.js
 * @description Array.prototype.lastIndexOf - value of 'length' is a string that can't convert to a number
 */


function testcase() {
        var targetObj = new String("123abc123");
        var obj = { 0: targetObj, length: "123abc123" };

        return Array.prototype.lastIndexOf.call(obj, targetObj) === -1;
    }
runTestCase(testcase);
