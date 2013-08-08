/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-11.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is own accessor property that overrides an inherited data property on an Array
 */


function testcase() {

        var arr = [];
        try {
            Array.prototype[0] = false;
            Object.defineProperty(arr, "0", {
                get: function () {
                    return true;
                },
                configurable: true
            });

            return 0 === arr.lastIndexOf(true);
        } finally {
            delete Array.prototype[0]; 
        }
    }
runTestCase(testcase);
