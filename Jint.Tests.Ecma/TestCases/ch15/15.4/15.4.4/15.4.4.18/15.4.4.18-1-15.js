/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-15.js
 * @description Array.prototype.forEach applied to the Arguments object
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = ('[object Arguments]' === Object.prototype.toString.call(obj));
        }

        var obj = (function () {
            return arguments;
        }("a", "b"));

        Array.prototype.forEach.call(obj, callbackfn);
        return result;
    }
runTestCase(testcase);
