/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-17.js
 * @description Array.prototype.reduceRight - 'accumulator' used for current iteration is the result of previous iteration on an Array
 */


function testcase() {

        var arr = [11, 12, 13];
        var testResult = true;
        var initVal = 6.99;
        var preResult = initVal;

        function callbackfn(prevVal, curVal, idx, obj) {
            if (prevVal !== preResult) {
                testResult = false;
            }
            preResult = curVal;
            return curVal;
        }

        arr.reduceRight(callbackfn, initVal);

        return testResult;
    }
runTestCase(testcase);
