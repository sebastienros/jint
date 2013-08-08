/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-i-8.js
 * @description Array.prototype.reduceRight - element to be retrieved is inherited data property on an Array
 */


function testcase() {

        var testResult = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 1) {
                testResult = (curVal === 1);
            }
        }

        try {
            Array.prototype[0] = 0;
            Array.prototype[1] = 1;
            Array.prototype[2] = 2;
            [, , , ].reduceRight(callbackfn, "initialValue");
            return testResult;

        } finally {
            delete Array.prototype[0];
            delete Array.prototype[1];
            delete Array.prototype[2];
        }
    }
runTestCase(testcase);
