/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-20.js
 * @description Array.prototype.every - callbackfn called with correct parameters (thisArg is correct)
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return 10 === this.threshold;
        }

        var thisArg = { threshold: 10 };

        var obj = { 0: 11, length: 1 };

        return Array.prototype.every.call(obj, callbackfn, thisArg);
    }
runTestCase(testcase);
