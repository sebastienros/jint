/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-7.js
 * @description Array.prototype.every applied to string primitive
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return !(obj instanceof String);
        }

        return !Array.prototype.every.call("hello\nworld\\!", callbackfn);
    }
runTestCase(testcase);
