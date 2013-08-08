/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-15.js
 * @description Array.prototype.some - Date object can be used as thisArg
 */


function testcase() {

        var objDate = new Date();

        function callbackfn(val, idx, obj) {
            return this === objDate;
        }

        return [11].some(callbackfn, objDate);
    }
runTestCase(testcase);
