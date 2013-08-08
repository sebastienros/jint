/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-10.js
 * @description Array.prototype.every - callbackfn is called with 1 formal parameter
 */


function testcase() {

        var called = 0;

        function callbackfn(val) {
            called++;
            return val > 10;
        }

        return [11, 12].every(callbackfn) && 2 === called;
    }
runTestCase(testcase);
