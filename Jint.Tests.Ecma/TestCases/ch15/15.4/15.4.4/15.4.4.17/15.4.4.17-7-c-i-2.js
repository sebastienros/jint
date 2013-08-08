/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-2.js
 * @description Array.prototype.some - element to be retrieved is own data property on an Array
 */


function testcase() {

        var kValue = {};

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return kValue === val;
            }
            return false;
        }

        return [kValue].some(callbackfn);
    }
runTestCase(testcase);
