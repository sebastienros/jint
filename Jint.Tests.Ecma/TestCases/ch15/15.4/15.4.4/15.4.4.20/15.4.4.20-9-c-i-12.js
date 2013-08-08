/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-12.js
 * @description  Array.prototype.filter - element to be retrieved is own accessor property that overrides an inherited data property on an Array
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return val === 111 && idx === 0;
        }

        var arr = [];
        try {
            Array.prototype[0] = 10;

            Object.defineProperty(arr, "0", {
                get: function () {
                    return 111;
                },
                configurable: true
            });
            var newArr = arr.filter(callbackfn);

            return newArr.length === 1 && newArr[0] === 111;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
