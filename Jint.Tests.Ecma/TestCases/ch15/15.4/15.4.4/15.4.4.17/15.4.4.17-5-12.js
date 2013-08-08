/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-12.js
 * @description Array.prototype.some - Boolean object can be used as thisArg
 */


function testcase() {

        var objBoolean = new Boolean();

        function callbackfn(val, idx, obj) {
            return this === objBoolean;
        }

        return [11].some(callbackfn, objBoolean);
    }
runTestCase(testcase);
