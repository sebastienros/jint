/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-20.js
 * @description Array.prototype.forEach - callbackfn called with correct parameters (thisArg is correct)
 */


function testcase() {

        var result = false;
        function callbackfn(val, idx, obj) {
            result = (10 === this.threshold);
        }

        var thisArg = { threshold: 10 };

        var obj = { 0: 11, length: 1 };

        Array.prototype.forEach.call(obj, callbackfn, thisArg);
        return result;
    }
runTestCase(testcase);
