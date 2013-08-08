/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-9.js
 * @description Array.prototype.forEach - callbackfn is called with 0 formal parameter
 */


function testcase() {

        var called = 0;
        function callbackfn() {
            called++;
        }

        [11, 12].forEach(callbackfn);
        return 2 === called;
    }
runTestCase(testcase);
