/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-10.js
 * @description Array.prototype.some - Array Object can be used as thisArg
 */


function testcase() {

        var objArray = [];

        function callbackfn(val, idx, obj) {
            return this === objArray;
        }

        return [11].some(callbackfn, objArray);
    }
runTestCase(testcase);
