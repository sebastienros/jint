/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-2.js
 * @description Array.prototype.filter - added properties in step 2 are visible here
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }

        var obj = {};

        Object.defineProperty(obj, "length", {
            get: function () {
                obj[2] = "length";
                return 3;
            },
            configurable: true
        });

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 1 && newArr[0] === "length";
    }
runTestCase(testcase);
