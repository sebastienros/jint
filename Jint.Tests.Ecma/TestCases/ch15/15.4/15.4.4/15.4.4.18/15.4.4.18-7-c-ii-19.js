/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-19.js
 * @description Array.prototype.forEach - non-indexed properties are not called
 */


function testcase() {

        var accessed = false;
        var result = true;
        function callbackfn(val, idx, obj) {
            accessed = true;
            if (val === 8) {
                result = false;
            }
        }

        var obj = { 0: 11, 10: 12, non_index_property: 8, length: 20 };

        Array.prototype.forEach.call(obj, callbackfn);
        return result && accessed;
    }
runTestCase(testcase);
