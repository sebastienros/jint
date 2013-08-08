/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-9.js
 * @description Array.prototype.forEach - Function Object can be used as thisArg
 */


function testcase() {

        var result = false;
        var objString = function () { };

        function callbackfn(val, idx, obj) {
            result = (this === objString);
        }

        [11].forEach(callbackfn, objString);
        return result;
    }
runTestCase(testcase);
