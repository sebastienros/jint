/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-13.js
 * @description Array.prototype.every - Number Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objNumber = new Number();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objNumber;
        }

        return [11].every(callbackfn, objNumber) && accessed;
    }
runTestCase(testcase);
