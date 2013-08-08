/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-7.js
 * @description Array.prototype.filter applied to string primitive
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return obj instanceof String;
        }

        var newArr = Array.prototype.filter.call("abc", callbackfn);

        return newArr[0] === "a";
    }
runTestCase(testcase);
