/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-1-1.js
 * @description Array.prototype.filter - value of returned array element equals to 'kValue'
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }

        var obj = { 0: 11, 1: 9, length: 2 };
        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr[0] === obj[0] && newArr[1] === obj[1];
    }
runTestCase(testcase);
