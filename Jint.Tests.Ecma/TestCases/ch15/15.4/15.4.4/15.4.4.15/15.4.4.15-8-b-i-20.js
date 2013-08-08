/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-20.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is an own accessor property without a get function that overrides an inherited accessor property on an Array
 */


function testcase() {

        var arr = [, 1];
        try {
            Object.defineProperty(Array.prototype, "0", {
                get: function () {
                    return 100;
                },
                configurable: true
            });
            Object.defineProperty(arr, "0", {
                set: function () { },
                configurable: true
            });

            return arr.hasOwnProperty(0) && arr.lastIndexOf(undefined) === 0;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
