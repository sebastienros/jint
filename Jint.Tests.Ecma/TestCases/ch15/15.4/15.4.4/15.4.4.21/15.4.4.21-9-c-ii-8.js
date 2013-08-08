/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-8.js
 * @description Array.prototype.reduce - element changed by callbackfn on previous iterations is observed
 */


function testcase() {

        var result = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            if (idx === 0) {
                obj[idx + 1] = 8;
            }
            
            if (idx === 1) {
                result = (curVal === 8);
            }
        }

        var obj = { 0: 11, 1: 12, length: 2 };

        Array.prototype.reduce.call(obj, callbackfn, 1);
        return result;
    }
runTestCase(testcase);
