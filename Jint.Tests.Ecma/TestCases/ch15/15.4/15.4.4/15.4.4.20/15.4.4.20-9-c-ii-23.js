/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-23.js
 * @description Array.prototype.filter - callbackfn called with correct parameters (this object O is correct)
 */


function testcase() {

        var obj = { 0: 11, length: 2 };

        function callbackfn(val, idx, o) {
            return obj === o;
        }

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
