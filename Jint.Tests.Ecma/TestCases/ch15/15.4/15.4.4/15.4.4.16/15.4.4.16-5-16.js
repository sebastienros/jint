/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-16.js
 * @description Array.prototype.every - RegExp Object can be used as thisArg
 */


function testcase() {

        var accessed = false;
        var objRegExp = new RegExp();

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this === objRegExp;
        }

        return [11].every(callbackfn, objRegExp) && accessed;
    }
runTestCase(testcase);
