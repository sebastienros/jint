/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-14.js
 * @description Object.keys - own enumerable indexed accessor property of sparse array 'O' is defined in returned array
 */


function testcase() {
        var obj = [1, , 3, , 5];

        Object.defineProperty(obj, "10000", {
            get: function () {
                return "ElementWithLargeIndex";
            },
            enumerable: true,
            configurable: true
        });

        var arr = Object.keys(obj);

        for (var p in arr) {
            if (arr[p] === "10000") {
                return true;
            }
        }

        return false;
    }
runTestCase(testcase);
