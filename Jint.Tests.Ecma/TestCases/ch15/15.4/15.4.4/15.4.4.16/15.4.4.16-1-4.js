/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-4.js
 * @description Array.prototype.every applied to Boolean object
 */


function testcase() {
        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
            return obj instanceof Boolean;
        }

        var obj = new Boolean(true);
        obj.length = 2;
        obj[0] = 11;
        obj[1] = 12;
        return Array.prototype.every.call(obj, callbackfn) && accessed;
    }
runTestCase(testcase);
