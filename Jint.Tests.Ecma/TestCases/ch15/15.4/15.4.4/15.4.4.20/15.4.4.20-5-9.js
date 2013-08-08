/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-9.js
 * @description Array.prototype.filter - Function Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objFunction = function () { };

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objFunction;
        }

        var newArr = [11].filter(callbackfn, objFunction);

        return newArr[0] === 11 && accessed;
    }
runTestCase(testcase);
