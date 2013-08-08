/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-13.js
 * @description Array.prototype.forEach - Number Object can be used as thisArg
 */


function testcase() {

        var result = false;
        var objNumber = new Number();

        function callbackfn(val, idx, obj) {
            result = (this === objNumber);
        }

        [11].forEach(callbackfn, objNumber);
        return result;
    }
runTestCase(testcase);
