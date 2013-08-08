/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-3-29.js
 * @description Array.prototype.lastIndexOf - value of 'length' is boundary value (2^32 + 1)
 */


function testcase() {

        var targetObj = {};
        var obj = {
            0: targetObj,
            1: 4294967297,
            length: 4294967297
        };

        return Array.prototype.lastIndexOf.call(obj, targetObj) === 0 &&
            Array.prototype.lastIndexOf.call(obj, 4294967297) === -1;
    }
runTestCase(testcase);
