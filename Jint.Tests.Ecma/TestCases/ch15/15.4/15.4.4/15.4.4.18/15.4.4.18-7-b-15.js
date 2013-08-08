/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-15.js
 * @description Array.prototype.forEach - decreasing length of array with prototype property causes prototype index property to be visited
 */


function testcase() {

        var testResult = false;

        function callbackfn(val, idx, obj) {
            if (idx === 2 && val === "prototype") {
                testResult = true;
            }
        }
        var arr = [0, 1, 2];

        try {
            Object.defineProperty(Array.prototype, "2", {
                get: function () {
                    return "prototype";
                },
                configurable: true
            });

            Object.defineProperty(arr, "1", {
                get: function () {
                    arr.length = 2;
                    return 1;
                },
                configurable: true
            });

            arr.forEach(callbackfn);

            return testResult;
        } finally {
            delete Array.prototype[2];
        }
    }
runTestCase(testcase);
