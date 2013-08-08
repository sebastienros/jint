/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-8.js
 * @description Array.prototype.some - element changed by callbackfn on previous iterations is observed
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                obj[idx + 1] = 11;
            }
            return val > 10;
        }

        var obj = { 0: 9, 1: 8, length: 2 };

        return Array.prototype.some.call(obj, callbackfn);
    }
runTestCase(testcase);
