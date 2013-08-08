/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-12.js
 * @description Array.prototype.forEach - Boolean Object can be used as thisArg
 */


function testcase() {

        var result = false;
        var objBoolean = new Boolean();

        function callbackfn(val, idx, obj) {
            result = (this === objBoolean);
        }

        [11].forEach(callbackfn, objBoolean);
        return result;
    }
runTestCase(testcase);
