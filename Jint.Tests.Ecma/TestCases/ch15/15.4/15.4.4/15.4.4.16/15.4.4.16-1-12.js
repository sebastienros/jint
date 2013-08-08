/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-12.js
 * @description Array.prototype.every applied to RegExp object
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return !(obj instanceof RegExp);
        }

        var obj = new RegExp();
        obj.length = 1;
        obj[0] = 1;

        return !Array.prototype.every.call(obj, callbackfn);
    }
runTestCase(testcase);
