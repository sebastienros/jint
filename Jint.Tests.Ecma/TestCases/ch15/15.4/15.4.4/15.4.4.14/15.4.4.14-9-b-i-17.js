/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-17.js
 * @description Array.prototype.indexOf - element to be retrieved is own accessor property without a get function on an Array
 */


function testcase() {

        var arr = [];
        Object.defineProperty(arr, "0", {
            set: function () { },
            configurable: true
        });

        return arr.indexOf(undefined) === 0;
    }
runTestCase(testcase);
