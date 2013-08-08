/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-15.js
 * @description Array.prototype.every applied to the Arguments object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return ('[object Arguments]' !== Object.prototype.toString.call(obj));
        }

        var obj = (function fun() {
            return arguments;
        }("a", "b"));

        return !Array.prototype.every.call(obj, callbackfn);
    }
runTestCase(testcase);
