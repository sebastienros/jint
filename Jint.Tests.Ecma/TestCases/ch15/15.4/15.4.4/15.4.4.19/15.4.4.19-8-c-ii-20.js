/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-20.js
 * @description Array.prototype.map - callbackfn called with correct parameters (thisArg is correct)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this.threshold === 10;
        }

        var thisArg = { threshold: 10 };

        var obj = { 0: 11, 1: 9, length: 2 };

        var testResult = Array.prototype.map.call(obj, callbackfn, thisArg);

        return testResult[0] === true;
    }
runTestCase(testcase);
