/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-11.js
 * @description Array.prototype.filter - String Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objString = new String();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objString;
        }

        var newArr = [11].filter(callbackfn, objString);

        return newArr[0] === 11 && accessed;
    }
runTestCase(testcase);
