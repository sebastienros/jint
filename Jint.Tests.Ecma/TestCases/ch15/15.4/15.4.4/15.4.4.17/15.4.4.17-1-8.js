/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-8.js
 * @description Array.prototype.some applied to String object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof String;
        }

        var obj = new String("hello\nw_orld\\!");
        return Array.prototype.some.call(obj, callbackfn);
    }
runTestCase(testcase);
