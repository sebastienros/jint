/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-1-7.js
 * @description Array.prototype.some applied to applied to string primitive
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof String;
        }

        return Array.prototype.some.call("hello\nw_orld\\!", callbackfn);
    }
runTestCase(testcase);
