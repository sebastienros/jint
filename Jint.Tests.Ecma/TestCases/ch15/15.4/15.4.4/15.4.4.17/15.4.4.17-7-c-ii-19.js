/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-19.js
 * @description Array.prototype.some - non-indexed properties are not called
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            return val === 11;
        }

        var obj = { 0: 9, 10: 8, non_index_property: 11, length: 20 };

        return !Array.prototype.some.call(obj, callbackfn) && (2 === called);
    }
runTestCase(testcase);
