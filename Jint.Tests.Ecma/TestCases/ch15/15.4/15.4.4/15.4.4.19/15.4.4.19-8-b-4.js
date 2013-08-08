/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-4.js
 * @description Array.prototype.map - properties added into own object after current position are visited on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 1 && val === 1) {
                return false;
            } else {
                return true;
            }
        }

        var obj = { length: 2 };

        Object.defineProperty(obj, "0", {
            get: function () {
                Object.defineProperty(obj, "1", {
                    get: function () {
                        return 1;
                    },
                    configurable: true
                });
                return 0;
            },
            configurable: true
        });

        var testResult = Array.prototype.map.call(obj, callbackfn);
        return testResult[0] === true && testResult[1] === false;
    }
runTestCase(testcase);
