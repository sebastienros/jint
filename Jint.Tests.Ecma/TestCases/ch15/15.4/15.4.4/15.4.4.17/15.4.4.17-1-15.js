/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-15.js
 * @description Array.prototype.some applied to the Arguments object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return '[object Arguments]' === Object.prototype.toString.call(obj);
        }

        var obj = (function () {
            return arguments;
        }("a", "b"));

        return Array.prototype.some.call(obj, callbackfn);
    }
runTestCase(testcase);
