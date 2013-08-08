/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-12.js
 * @description Array.prototype.every - Boolean Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objBoolean = new Boolean();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objBoolean;
        }

       

        return [11].every(callbackfn, objBoolean) && accessed;
    }
runTestCase(testcase);
