/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-i-12.js
 * @description  Array.prototype.forEach - element to be retrieved is own accessor property that overrides an inherited data property on an Array
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                testResult = (val === 111);
            }
        }

        var arr = [];
        try {
            Array.prototype[0] = 10;

            Object.defineProperty(arr, "0", {
                get: function () {
                    return 111;
                },
                configurable: true
            });

            arr.forEach(callbackfn);

            return testResult;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
