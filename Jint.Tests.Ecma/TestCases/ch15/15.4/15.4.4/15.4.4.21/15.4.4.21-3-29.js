/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-29.js
 * @description Array.prototype.reduce - value of 'length' is boundary value (2^32 + 1)
 */


function testcase() {

        function callbackfn(prevVal, curVal, idx, obj) {
            return (curVal === 11 && idx === 0);
        }

        var obj = {
            0: 11,
            1: 9,
            length: 4294967297
        };

        return Array.prototype.reduce.call(obj, callbackfn, 1);
    }
runTestCase(testcase);
