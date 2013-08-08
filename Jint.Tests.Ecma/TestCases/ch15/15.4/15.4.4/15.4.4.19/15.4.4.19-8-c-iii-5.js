/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-iii-5.js
 * @description Array.prototype.map - value of returned array element can be changed or deleted
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return true;
        }

        var obj = { 0: 11, 1: 9, length: 2 };
        var newArr = Array.prototype.map.call(obj, callbackfn);

        try {
            var tempVal = newArr[1];
            delete newArr[1];
            return tempVal !== undefined && newArr[1] === undefined;
        } catch (ex) {
            return false;
        }
    }
runTestCase(testcase);
