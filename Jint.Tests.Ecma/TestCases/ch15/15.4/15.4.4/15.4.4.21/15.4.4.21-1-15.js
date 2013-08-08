/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-15.js
 * @description Array.prototype.reduce applied to the Arguments object
 */


function testcase() {

        function callbackfn(prevVal, curVal, idx, obj) {
            return ('[object Arguments]' === Object.prototype.toString.call(obj));
        }

        var obj = (function () {
            return arguments;
        }("a", "b"));

        return Array.prototype.reduce.call(obj, callbackfn, 1);
    }
runTestCase(testcase);
