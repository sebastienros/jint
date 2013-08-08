/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-10.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is own accessor property on an Array-like object
 */


function testcase() {

        var obj = { length: 3 };
        Object.defineProperty(obj, "0", {
            get: function () {
                return 0;
            },
            configurable: true
        });

        Object.defineProperty(obj, "1", {
            get: function () {
                return 1;
            },
            configurable: true
        });

        Object.defineProperty(obj, "2", {
            get: function () {
                return 2;
            },
            configurable: true
        });

        return 0 === Array.prototype.lastIndexOf.call(obj, 0) &&
            1 === Array.prototype.lastIndexOf.call(obj, 1) &&
            2 === Array.prototype.lastIndexOf.call(obj, 2);
    }
runTestCase(testcase);
