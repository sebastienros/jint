/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-b-i-17.js
 * @description Array.prototype.lastIndexOf - element to be retrieved is own accessor property without a get function on an Array
 */


function testcase() {

        var arr = [];
        Object.defineProperty(arr, "0", {
            set: function () { },
            configurable: true
        });

        return arr.lastIndexOf(undefined) === 0;
    }
runTestCase(testcase);
