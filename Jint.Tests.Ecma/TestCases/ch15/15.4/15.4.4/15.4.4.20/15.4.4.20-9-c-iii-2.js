/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-iii-2.js
 * @description Array.prototype.filter - return value of callbackfn is undefined
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, o) {
            accessed = true;
            return undefined;
        }

        var obj = { 0: 11, length: 1 };

        var newArr = Array.prototype.filter.call(obj, callbackfn);
        return  newArr.length === 0 && accessed;
    }
runTestCase(testcase);
