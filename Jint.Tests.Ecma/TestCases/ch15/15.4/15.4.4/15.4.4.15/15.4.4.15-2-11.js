/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-2-11.js
 * @description Array.prototype.lastIndexOf - 'length' is own accessor property without a get function on an Array-like object
 */


function testcase() {

        var obj = { 0: 1 };
        Object.defineProperty(obj, "length", {
            set: function () { },
            configurable: true
        });

        return Array.prototype.lastIndexOf.call(obj, 1) === -1;
    }
runTestCase(testcase);
