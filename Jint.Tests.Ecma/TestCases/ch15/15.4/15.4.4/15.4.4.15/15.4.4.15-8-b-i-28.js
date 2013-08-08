/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-28.js
 * @description Array.prototype.lastIndexOf - side-effects are visible in subsequent iterations on an Array
 */


function testcase() {

        var preIterVisible = false;
        var arr = [];

        Object.defineProperty(arr, "2", {
            get: function () {
                preIterVisible = true;
                return false;
            },
            configurable: true
        });

        Object.defineProperty(arr, "1", {
            get: function () {
                if (preIterVisible) {
                    return true;
                } else {
                    return false;
                }
            },
            configurable: true
        });

        return arr.lastIndexOf(true) === 1;
    }
runTestCase(testcase);
