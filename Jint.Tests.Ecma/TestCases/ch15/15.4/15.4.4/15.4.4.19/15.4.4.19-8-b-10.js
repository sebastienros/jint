/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-10.js
 * @description Array.prototype.map - deleting property of prototype causes prototype index property not to be visited on an Array-like Object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return idx === 1 && typeof val === "undefined";
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
            var testResult = Array.prototype.map.call(obj, callbackfn);
            return testResult.length === 20 && typeof testResult[1] === "undefined";
        } finally {
            delete Object.prototype[1];
        }
    }
runTestCase(testcase);
