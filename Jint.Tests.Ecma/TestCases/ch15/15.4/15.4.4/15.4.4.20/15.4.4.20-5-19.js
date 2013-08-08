/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-19.js
 * @description Array.prototype.filter - the Arguments object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var arg;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === arg;
        }

        (function fun() {
            arg = arguments;
        }(1, 2, 3));

        var newArr = [11].filter(callbackfn, arg);
        return newArr[0] === 11 && accessed;
    }
runTestCase(testcase);
