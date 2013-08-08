/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-15.js
 * @description Array.prototype.forEach - Date Object can be used as thisArg
 */


function testcase() {

        var result = false;
        var objDate = new Date();

        function callbackfn(val, idx, obj) {
            result = (this === objDate);
        }

        [11].forEach(callbackfn, objDate);
        return result;
    }
runTestCase(testcase);
