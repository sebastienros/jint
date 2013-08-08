/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-29.js
 * @description Array.prototype.indexOf - side-effects are visible in subsequent iterations on an Array-like object
 */


function testcase() {

        var preIterVisible = false;
        var obj = { length: 2 };

        Object.defineProperty(obj, "0", {
            get: function () {
                preIterVisible = true;
                return false;
            },
            configurable: true
        });

        Object.defineProperty(obj, "1", {
            get: function () {
                if (preIterVisible) {
                    return true;
                } else {
                    return false;
                }
            },
            configurable: true
        });

        return Array.prototype.indexOf.call(obj, true) === 1;
    }
runTestCase(testcase);
