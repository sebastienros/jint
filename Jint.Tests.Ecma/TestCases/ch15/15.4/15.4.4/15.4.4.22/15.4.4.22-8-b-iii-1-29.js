/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-29.js
 * @description Array.prototype.reduceRight applied to Function object which implements its own property get method
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === 2);
            }
        }

        var obj = function (a, b, c) {
            return a + b + c;
        };

        obj[0] = 0;
        obj[1] = 1;
        obj[2] = 2;

        Array.prototype.reduceRight.call(obj, callbackfn);
        return testResult;

    }
runTestCase(testcase);
