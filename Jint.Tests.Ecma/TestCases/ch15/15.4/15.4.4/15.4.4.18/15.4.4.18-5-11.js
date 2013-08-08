/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-11.js
 * @description Array.prototype.forEach - String Object can be used as thisArg
 */


function testcase() {

        var result = false;
        var objString = new String();

        function callbackfn(val, idx, obj) {
            result = (this === objString);
        }

        [11].forEach(callbackfn, objString);
        return result;
    }
runTestCase(testcase);
