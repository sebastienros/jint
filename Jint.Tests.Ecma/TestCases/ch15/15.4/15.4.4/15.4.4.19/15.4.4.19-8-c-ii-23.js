/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-23.js
 * @description Array.prototype.map - callbackfn called with correct parameters (this object O is correct)
 */


function testcase() {

        var obj = { 0: 11, length: 2 };

        function callbackfn(val, idx, o) {
            return obj === o;
        }

        var testResult = Array.prototype.map.call(obj, callbackfn);

        return testResult[0] === true;
    }
runTestCase(testcase);
