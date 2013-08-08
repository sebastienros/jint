/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-iii-2.js
 * @description Array.prototype.lastIndexOf returns without visiting subsequent element once search value is found
 */


function testcase() {
        var arr = [2, 1, , 1, 2];
        var elementFirstAccessed = false;
        var elementThirdAccessed = false;

        Object.defineProperty(arr, "2", {
            get: function () {
                elementThirdAccessed = true;
                return 2;
            },
            configurable: true
        });
        Object.defineProperty(arr, "0", {
            get: function () {
                elementFirstAccessed = true;
                return 2;
            },
            configurable: true
        });

        arr.lastIndexOf(2);
        return !elementThirdAccessed && !elementFirstAccessed;
    }
runTestCase(testcase);
