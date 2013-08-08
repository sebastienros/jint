/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-b-10.js
 * @description Array.prototype.filter - deleting property of prototype causes prototype index property not to be visited on an Array-like Object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }
        var obj = { 2: 2, length: 20 };

        Object.defineProperty(obj, "0", {
            get: function () {
                delete Object.prototype[1];
                return 0;
            },
            configurable: true
        });

        try {
            Object.prototype[1] = 1;
            var newArr = Array.prototype.filter.call(obj, callbackfn);

            return newArr.length === 2 && newArr[1] !== 1;
        } finally {
            delete Object.prototype[1];
        }
    }
runTestCase(testcase);
