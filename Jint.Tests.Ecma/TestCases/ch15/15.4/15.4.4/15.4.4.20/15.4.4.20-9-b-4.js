/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-4.js
 * @description Array.prototype.filter - properties added into own object after current position are visited on an Array-like object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }

        var obj = { length: 2 };

        Object.defineProperty(obj, "0", {
            get: function () {
                Object.defineProperty(obj, "1", {
                    get: function () {
                        return 6.99;
                    },
                    configurable: true
                });
                return 0;
            },
            configurable: true
        });

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 2 && newArr[1] === 6.99;
    }
runTestCase(testcase);
