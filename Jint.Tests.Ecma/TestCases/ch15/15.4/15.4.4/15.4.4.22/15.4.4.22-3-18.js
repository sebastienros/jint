/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-18.js
 * @description Array.prototype.reduceRight - value of 'length' is a string that can't convert to a number
 */


function testcase() {

        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 9, 1: 8, length: "two" };

        return Array.prototype.reduceRight.call(obj, callbackfn, 11) === 11 && !accessed;
    }
runTestCase(testcase);
