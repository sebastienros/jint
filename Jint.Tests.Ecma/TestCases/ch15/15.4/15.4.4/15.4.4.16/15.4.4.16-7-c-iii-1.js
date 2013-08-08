/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-iii-1.js
 * @description Array.prototype.every - return value of callbackfn is undefined
 */


function testcase() {

        var accessed = false;
        var obj = { 0: 11, length: 1 };

        function callbackfn(val, idx, o) {
            accessed = true;
            return undefined;
        }

        

        return !Array.prototype.every.call(obj, callbackfn) && accessed;
    }
runTestCase(testcase);
