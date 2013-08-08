/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-16.js
 * @description Array.prototype.some - RegExp object can be used as thisArg
 */


function testcase() {

        var objRegExp = new RegExp();

        function callbackfn(val, idx, obj) {
            return this === objRegExp;
        }

        return [11].some(callbackfn, objRegExp);
    }
runTestCase(testcase);
