/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-17.js
 * @description Array.prototype.filter - element to be retrieved is own accessor property without a get function on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return undefined === val && idx === 1;
        }

        var obj = { length: 2 };
        Object.defineProperty(obj, "1", {
            set: function () { },
            configurable: true
        });

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 1 && newArr[0] === undefined;
    }
runTestCase(testcase);
