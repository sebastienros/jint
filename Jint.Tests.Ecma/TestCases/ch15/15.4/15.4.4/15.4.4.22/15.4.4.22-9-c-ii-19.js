/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-19.js
 * @description Array.prototype.reduceRight - value of 'accumulator' used for first iteration is the value of max index property which is not undefined when 'initialValue' is not present on an Array
 */


function testcase() {

        var arr = [11, 12, 13];
        var testResult = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === 13);
            }
            return curVal;
        }
        arr.reduceRight(callbackfn);

        return testResult;
    }
runTestCase(testcase);
