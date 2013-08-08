/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-5.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is own data property that overrides an inherited accessor property on an Array
 */


function testcase() {
        try {
            Object.defineProperty(Array.prototype, "0", {
                get: function () {
                    return false;
                },
                configurable: true
            });
            return 0 === [Number].lastIndexOf(Number);
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
