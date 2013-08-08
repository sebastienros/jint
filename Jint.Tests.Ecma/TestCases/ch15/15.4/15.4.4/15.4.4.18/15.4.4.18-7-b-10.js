/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-b-10.js
 * @description Array.prototype.forEach - deleting property of prototype causes prototype index property not to be visited on an Array-like Object
 */


function testcase() {

        var accessed = false;
        var testResult = true;

        function callbackfn(val, idx, obj) {
            accessed = true;
            if (idx === 3) {
                testResult = false;
            }
        }

        var obj = { 2: 2, length: 20 };

        Object.defineProperty(obj, "0", {
            get: function () {
                delete Object.prototype[1];
                return 0;
            },
            configurable: true
        });

        try {
            Object.prototype[1] = 1;
            Array.prototype.forEach.call(obj, callbackfn);
            return testResult && accessed;
        } finally {
            delete Object.prototype[1];
        }
    }
runTestCase(testcase);
