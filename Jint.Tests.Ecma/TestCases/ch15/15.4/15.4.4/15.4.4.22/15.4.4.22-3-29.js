/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-3-29.js
 * @description Array.prototype.reduceRight - value of 'length' is boundary value (2^32 + 1)
 */


function testcase() {

        var testResult1 = true;
        var testResult2 = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx > 0) {
                testResult1 = false;
            }

            if (idx === 0) {
                testResult2 = true;
            }
            return false;
        }

        var obj = {
            0: 11,
            1: 9,
            length: 4294967297
        };

        Array.prototype.reduceRight.call(obj, callbackfn, 1);
        return testResult1 && testResult2;
    }
runTestCase(testcase);
