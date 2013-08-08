/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-29.js
 * @description Array.prototype.filter - element changed by getter on previous iterations is observed on an Array-like object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return val === 9 && idx === 1;
        }

        var preIterVisible = false;
        var obj = { length: 2 };

        Object.defineProperty(obj, "0", {
            get: function () {
                preIterVisible = true;
                return 11;
            },
            configurable: true
        });

        Object.defineProperty(obj, "1", {
            get: function () {
                if (preIterVisible) {
                    return 9;
                } else {
                    return 13;
                }
            },
            configurable: true
        });
        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 1 && newArr[0] === 9;
    }
runTestCase(testcase);
