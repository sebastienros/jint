/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-13.js
 * @description Array.prototype.filter - Number Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objNumber = new Number();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objNumber;
        }

        var newArr = [11].filter(callbackfn, objNumber);

        return newArr[0] === 11 && accessed;
    }
runTestCase(testcase);
