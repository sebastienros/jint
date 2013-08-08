/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-29.js
 * @description Array.prototype.filter - false prevents element added to output Array
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            return val > 10;
        }

        var obj = { 0: 11, 1: 8, length: 20 };

        var newArr = Array.prototype.filter.call(obj, callbackfn);
        return newArr.length === 1 && newArr[0] !== 8 && called === 2;

    }
runTestCase(testcase);
