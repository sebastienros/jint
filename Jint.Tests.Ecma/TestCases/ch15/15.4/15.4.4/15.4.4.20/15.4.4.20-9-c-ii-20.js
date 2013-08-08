/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-20.js
 * @description Array.prototype.filter - callbackfn called with correct parameters (thisArg is correct)
 */


function testcase() {

        var thisArg = { threshold: 10 };

        function callbackfn(val, idx, obj) {
            return this === thisArg;
        }

        var obj = { 0: 11, length: 1 };
        var newArr = Array.prototype.filter.call(obj, callbackfn, thisArg);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
