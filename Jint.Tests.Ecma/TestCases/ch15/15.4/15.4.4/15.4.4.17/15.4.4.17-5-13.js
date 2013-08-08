/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-13.js
 * @description Array.prototype.some - Number object can be used as thisArg
 */


function testcase() {

        var objNumber = new Number();

        function callbackfn(val, idx, obj) {
            return this === objNumber;
        }

        return [11].some(callbackfn, objNumber);
    }
runTestCase(testcase);
