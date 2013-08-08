/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-191.js
 * @description Object.defineProperties - 'O' is an Array, 'P' is an array index property, 'P' is inherited accessor property  (15.4.5.1 step 4.c)
 */


function testcase() {
        try {
            Object.defineProperty(Array.prototype, "0", {
                get: function () {
                    return 11;
                },
                configurable: true
            });

            var arr = [];

            Object.defineProperties(arr, {
                "0": {
                    get: function () {
                        return 12;
                    },
                    configurable: false
                }
            });
            return arr.hasOwnProperty("0") && arr[0] === 12 && Array.prototype[0] === 11;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
