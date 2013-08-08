/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-6.js
 * @description Array.prototype.some - element to be retrieved is own data property that overrides an inherited accessor property on an Array
 */


function testcase() {

        var kValue = 1000;

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val === kValue;
            }
            return false;
        }

        try {
            Object.defineProperty(Array.prototype, "0", {
                get: function () {
                    return 9;
                },
                configurable: true
            });
            return [kValue].some(callbackfn);
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
