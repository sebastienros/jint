/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-15.js
 * @description Array.prototype.every - Date Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objDate = new Date();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objDate;
        }

        return [11].every(callbackfn, objDate) && accessed;
    }
runTestCase(testcase);
