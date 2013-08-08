/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-10.js
 * @description Array.prototype.every - Array Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objArray = [];

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objArray;
        }



        return [11].every(callbackfn, objArray) && accessed;
    }
runTestCase(testcase);
