/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-7.js
 * @description Array.prototype.lastIndexOf - 'length' is own accessor property on an Array-like object
 */


function testcase() {

        var obj = { 1: true, 2: false };

        Object.defineProperty(obj, "length", {
            get: function () {
                return 2;
            },
            configurable: true
        });

        return Array.prototype.lastIndexOf.call(obj, true) === 1 &&
            Array.prototype.lastIndexOf.call(obj, false) === -1;
    }
runTestCase(testcase);
