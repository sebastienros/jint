/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-21.js
 * @description Array.prototype.reduceRight - deleting own property causes deleted index property not to be visited on an Array-like object
 */


function testcase() {

        var accessed = false;
        var testResult = true;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            if (idx === 1) {
                testResult = false;
            }
        }

        var obj = { 0: 10, length: 10 };

        Object.defineProperty(obj, "1", {
            get: function () {
                return 6.99;
            },
            configurable: true
        });

        Object.defineProperty(obj, "5", {
            get: function () {
                delete obj[1];
                return 0;
            },
            configurable: true
        });

        Array.prototype.reduceRight.call(obj, callbackfn, "initialValue");
        return testResult && accessed;
    }
runTestCase(testcase);
