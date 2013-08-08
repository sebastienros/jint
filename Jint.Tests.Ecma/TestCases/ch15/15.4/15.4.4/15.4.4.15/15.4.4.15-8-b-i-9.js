/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-9.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is own accessor property on an Array
 */


function testcase() {

        var arr = [, , , ];
        Object.defineProperty(arr, "0", {
            get: function () {
                return 0;
            },
            configurable: true
        });

        Object.defineProperty(arr, "1", {
            get: function () {
                return 1;
            },
            configurable: true
        });

        Object.defineProperty(arr, "2", {
            get: function () {
                return 2;
            },
            configurable: true
        });

        return arr.lastIndexOf(0) === 0 && arr.lastIndexOf(1) === 1 && arr.lastIndexOf(2) === 2;
    }
runTestCase(testcase);
