/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-12.js
 * @description  Array.prototype.every - element to be retrieved is own accessor property that overrides an inherited data property on an Array
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val === 10;
            } else {
                return true;
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

            return !arr.every(callbackfn);
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
