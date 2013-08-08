/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-4-11.js
 * @description Array.prototype.lastIndexOf - 'length' is an empty string
 */


function testcase() {
        var targetObj = [];
        var obj = { 0: targetObj, 100: targetObj, length: "" };
        return Array.prototype.lastIndexOf.call(obj, targetObj) === -1;
    }
runTestCase(testcase);
