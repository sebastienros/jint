/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-11.js
 * @description Array.prototype.indexOf - 'length' is own accessor property without a get function
 */


function testcase() {

        var obj = { 1: true };
        Object.defineProperty(obj, "length", {
            set: function () { },
            configurable: true
        });

        return Array.prototype.indexOf.call(obj, true) === -1;
    }
runTestCase(testcase);
