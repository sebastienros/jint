/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-7.js
 * @description Array.prototype.reduce applied to string primitive
 */


function testcase() {

        function callbackfn(prevVal, curVal, idx, obj) {
            return obj instanceof String;
        }

        return Array.prototype.reduce.call("abc", callbackfn, 1);
    }
runTestCase(testcase);
