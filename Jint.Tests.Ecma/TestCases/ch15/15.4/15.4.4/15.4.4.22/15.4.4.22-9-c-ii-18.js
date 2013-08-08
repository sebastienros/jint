/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-18.js
 * @description Array.prototype.reduceRight - 'accumulator' used for first iteration is the value of 'initialValue' when it is present on an Array
 */


function testcase() {

        var arr = [11, 12];
        var testResult = false;
        var initVal = 6.99;

        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === initVal);
            }
            return curVal;
        }

        arr.reduceRight(callbackfn, initVal);

        return testResult;
    }
runTestCase(testcase);
