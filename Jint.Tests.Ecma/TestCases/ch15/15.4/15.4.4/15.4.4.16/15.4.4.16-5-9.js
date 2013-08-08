/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-9.js
 * @description Array.prototype.every - Function Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objFunction = function () { };

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objFunction;
        }

        return [11].every(callbackfn, objFunction) && accessed;
    }
runTestCase(testcase);
