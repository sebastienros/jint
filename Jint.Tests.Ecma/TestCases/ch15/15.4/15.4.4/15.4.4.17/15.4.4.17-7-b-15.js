/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-b-15.js
 * @description Array.prototype.some - decreasing length of array with prototype property causes prototype index property to be visited
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            if (idx === 2 && val === "prototype") {
                return true;
            } else {
                return false;
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

            return arr.some(callbackfn);
        } finally {
            delete Array.prototype[2];
        }
    }
runTestCase(testcase);
