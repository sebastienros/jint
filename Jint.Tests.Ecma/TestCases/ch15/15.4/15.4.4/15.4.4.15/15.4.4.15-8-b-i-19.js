/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-19.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is own accessor property without a get function that overrides an inherited accessor property on an Array-like object
 */


function testcase() {

        var obj = { length: 1 };
        try {
            Object.defineProperty(Object.prototype, "0", {
                get: function () {
                    return 20;
                },
                configurable: true
            });
            Object.defineProperty(obj, "0", {
                set: function () { },
                configurable: true
            });

            return obj.hasOwnProperty(0) && 0 === Array.prototype.lastIndexOf.call(obj, undefined);
        } finally {
            delete Object.prototype[0];
        }
    }
runTestCase(testcase);
