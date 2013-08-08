/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-3.js
 * @description Array.prototype.reduceRight - deleted properties in step 2 are visible here
 */


function testcase() {

        var accessed = false;
        var testResult = true;

        function callbackfn(preVal, curVal, idx, obj) {
            accessed = true;
            if (idx === 2) {
                testResult = false;
            }
        }

        var obj = { 2: "2", 3: 10 };

        Object.defineProperty(obj, "length", {
            get: function () {
                delete obj[2];
                return 5;
            },
            configurable: true
        });

        Array.prototype.reduceRight.call(obj, callbackfn, "initialValue");

        return accessed && testResult;
    }
runTestCase(testcase);
