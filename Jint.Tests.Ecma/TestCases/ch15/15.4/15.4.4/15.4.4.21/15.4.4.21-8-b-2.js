/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-8-b-2.js
 * @description Array.prototype.reduce - modifications to length don't change number of iterations in step 9
 */


function testcase() {

        function callbackfn(prevVal, curVal, idx, obj) {
            return idx;
        }

        var obj = { 3: 12, 4: 9, length: 4 };

        Object.defineProperty(obj, "2", {
            get: function () {
                obj.length = 10;
                return 11;
            },
            configurable: true
        });

        return Array.prototype.reduce.call(obj, callbackfn) === 3;
    }
runTestCase(testcase);
