/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-19.js
 * @description Array.prototype.forEach - the Arguments object can be used as thisArg
 */


function testcase() {

        var result = false;
        var arg;

        function callbackfn(val, idx, obj) {
            result = (this === arg);
        }

        (function fun() {
            arg = arguments;
        }(1, 2, 3));

        [11].forEach(callbackfn, arg);
        return result;
    }
runTestCase(testcase);
