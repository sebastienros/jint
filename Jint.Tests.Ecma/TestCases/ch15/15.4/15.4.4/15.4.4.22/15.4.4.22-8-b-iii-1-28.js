/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-8-b-iii-1-28.js
 * @description Array.prototype.reduceRight applied to String object, which implements its own property get method
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (prevVal === "2");
            }
        }

        var str = new String("012");

        Array.prototype.reduceRight.call(str, callbackfn);
        return testResult;

    }
runTestCase(testcase);
