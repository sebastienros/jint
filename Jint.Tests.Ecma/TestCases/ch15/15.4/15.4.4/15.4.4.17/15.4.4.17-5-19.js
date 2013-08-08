/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-19.js
 * @description Array.prototype.some - the Arguments object can be used as thisArg
 */


function testcase() {

        var arg;

        function callbackfn(val, idx, obj) {
            return this === arg;
        }

        (function fun() {
            arg = arguments;
        }(1, 2, 3));

        return [11].some(callbackfn, arg);
    }
runTestCase(testcase);
