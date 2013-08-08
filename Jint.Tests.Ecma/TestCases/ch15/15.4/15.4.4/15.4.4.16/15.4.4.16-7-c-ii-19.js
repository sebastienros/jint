/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-19.js
 * @description Array.prototype.every - non-indexed properties are not called
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            return val !== 8;
        }

        var obj = { 0: 11, 10: 12, non_index_property: 8, length: 20 };

        return Array.prototype.every.call(obj, callbackfn) && 2 === called;
    }
runTestCase(testcase);
