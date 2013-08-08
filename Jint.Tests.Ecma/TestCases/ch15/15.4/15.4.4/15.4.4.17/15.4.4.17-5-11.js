/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-11.js
 * @description Array.prototype.some - String object can be used as thisArg
 */


function testcase() {

        var objString = new String();

        function callbackfn(val, idx, obj) {
            return this === objString;
        }

        return [11].some(callbackfn, objString);
    }
runTestCase(testcase);
