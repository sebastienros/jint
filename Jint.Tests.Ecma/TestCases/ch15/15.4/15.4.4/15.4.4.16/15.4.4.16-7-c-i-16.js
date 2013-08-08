/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-16.js
 * @description Array.prototype.every - element to be retrieved is inherited accessor property on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val !== 11;
            } else {
                return true;
            }
        }

        try {
            Object.defineProperty(Array.prototype, "0", {
                get: function () {
                    return 11;
                },
                configurable: true
            });

            return ![, , , ].every(callbackfn);
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
