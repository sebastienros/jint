/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-16.js
 * @description Array.prototype.forEach - RegExp Object can be used as thisArg
 */


function testcase() {

        var result = false;
        var objRegExp = new RegExp();

        function callbackfn(val, idx, obj) {
            result = (this === objRegExp);
        }

        [11].forEach(callbackfn, objRegExp);
        return result;
    }
runTestCase(testcase);
