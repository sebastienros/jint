/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1-3.js
 * @description Array.prototype.filter - value of returned array element can be enumerated
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }

        var obj = { 0: 11, length: 2 };
        var newArr = Array.prototype.filter.call(obj, callbackfn);

        var prop;
        var enumerable = false;
        for (prop in newArr) {
            if (newArr.hasOwnProperty(prop)) {
                if (prop === "0") {
                    enumerable = true;
                }
            }
        }

        return enumerable;
    }
runTestCase(testcase);
