/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-22.js
 * @description Array.prototype.every - boolean primitive can be used as thisArg
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this.valueOf() === false;
        }

        return [11].every(callbackfn, false) && accessed;
    }
runTestCase(testcase);
