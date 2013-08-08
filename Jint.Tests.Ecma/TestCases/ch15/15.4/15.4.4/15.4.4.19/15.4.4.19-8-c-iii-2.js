/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-iii-2.js
 * @description Array.prototype.map - value of returned array element equals to 'mappedValue'
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val;
        }

        var obj = { 0: 11, 1: 9, length: 2 };
        var newArr = Array.prototype.map.call(obj, callbackfn);

        return newArr[0] === obj[0] && newArr[1] === obj[1];
    }
runTestCase(testcase);
