/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-13.js
 * @description Array.prototype.forEach - callbackfn that uses arguments
 */


function testcase() {

        var result = false;
        function callbackfn() {
            result = (arguments[2][arguments[1]] === arguments[0]);
        }

        [11].forEach(callbackfn);
        return result;
    }
runTestCase(testcase);
