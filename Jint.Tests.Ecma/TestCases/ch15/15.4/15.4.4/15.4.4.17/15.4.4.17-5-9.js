/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-9.js
 * @description Array.prototype.some - Function Object can be used as thisArg
 */


function testcase() {

        var objFunction = function () { };

        function callbackfn(val, idx, obj) {
            return this === objFunction;
        }

        return [11].some(callbackfn, objFunction);
    }
runTestCase(testcase);
