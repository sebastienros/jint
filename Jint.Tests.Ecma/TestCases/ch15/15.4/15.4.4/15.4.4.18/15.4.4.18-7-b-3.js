/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-3.js
 * @description Array.prototype.forEach - deleted properties in step 2 are visible here
 */


function testcase() {

        var accessed = false;
        var testResult = true;

        function callbackfn(val, idx, obj) {
            accessed = true;
            if (idx === 8) {
                testResult = false;
            }
        }
        var obj = { 2: 6.99, 8: 19 };

        Object.defineProperty(obj, "length", {
            get: function () {
                delete obj[8];
                return 10;
            },
            configurable: true
        });

        Array.prototype.forEach.call(obj, callbackfn);
        return testResult && accessed;
    }
runTestCase(testcase);
