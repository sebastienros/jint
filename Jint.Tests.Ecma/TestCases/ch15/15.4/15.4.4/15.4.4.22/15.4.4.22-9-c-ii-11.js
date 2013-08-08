/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-11.js
 * @description Array.prototype.reduceRight - callbackfn is called with 2 formal parameter
 */


function testcase() {

        var testResult = false;

        function callbackfn(prevVal, curVal) {
            if (prevVal === 100) {
                testResult = true;
            }
            return curVal > 10;
        }

        return [11].reduceRight(callbackfn, 100) === true && testResult;
    }
runTestCase(testcase);
