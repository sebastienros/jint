/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-12.js
 * @description Array.prototype.filter - Boolean Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objBoolean = new Boolean();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objBoolean;
        }

        var newArr = [11].filter(callbackfn, objBoolean);

        return newArr[0] === 11 && accessed;
    }
runTestCase(testcase);
