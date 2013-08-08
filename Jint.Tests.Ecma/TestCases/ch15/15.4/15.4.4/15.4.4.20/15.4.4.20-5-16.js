/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-16.js
 * @description Array.prototype.filter - RegExp Object can be used as thisArg
 */


function testcase() {

        var accessed = false;

        var objRegExp = new RegExp();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objRegExp;
        }

        var newArr = [11].filter(callbackfn, objRegExp);

        return newArr[0] === 11 && accessed;
    }
runTestCase(testcase);
