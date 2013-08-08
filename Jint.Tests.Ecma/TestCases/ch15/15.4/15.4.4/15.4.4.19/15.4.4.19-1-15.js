/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-15.js
 * @description Array.prototype.map - applied to the Arguments object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return ('[object Arguments]' === Object.prototype.toString.call(obj));
        }

        var obj = (function () {
            return arguments;
        }("a", "b"));

        var testResult = Array.prototype.map.call(obj, callbackfn);

        return testResult[1] === true;
    }
runTestCase(testcase);
