/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-8.js
 * @description Array.prototype.every applied to String object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return !(obj instanceof String);
        }

        var obj = new String("hello\nworld\\!");

        return !Array.prototype.every.call(obj, callbackfn);
    }
runTestCase(testcase);
