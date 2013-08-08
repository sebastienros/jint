/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-15.js
 * @description Array.prototype.filter applied to the Arguments object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return '[object Arguments]' === Object.prototype.toString.call(obj);
        }

        var obj = (function () {
            return arguments;
        }("a", "b"));

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return newArr[0] === "a" && newArr[1] === "b";
    }
runTestCase(testcase);
