/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-16.js
 * @description Array.prototype.some - element to be retrieved is inherited accessor property on an Array
 */


function testcase() {

        var kValue = "abc";

        function callbackfn(val, idx, obj) {
            if (idx === 1) {
                return val === kValue;
            }
            return false;
        }

        try {
            Object.defineProperty(Array.prototype, "1", {
                get: function () {
                    return kValue;
                },
                configurable: true
            });

            return [, , ].some(callbackfn);
        } finally {
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);
