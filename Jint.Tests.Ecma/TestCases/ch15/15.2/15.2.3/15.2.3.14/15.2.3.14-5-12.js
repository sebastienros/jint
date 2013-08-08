/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-5-12.js
 * @description Object.keys - own enumerable indexed accessor property of dense array 'O' is defined in returned array
 */


function testcase() {
        var obj = [2, 3, 4, 5];

        Object.defineProperty(obj, "prop", {
            get: function () {
                return 6;
            },
            enumerable: true,
            configurable: true
        });

        var arr = Object.keys(obj);

        for (var p in arr) {
            if (arr.hasOwnProperty(p)) {
                if (arr[p] === "prop") {
                    return true;
                }
            }
        }

        return false;
    }
runTestCase(testcase);
