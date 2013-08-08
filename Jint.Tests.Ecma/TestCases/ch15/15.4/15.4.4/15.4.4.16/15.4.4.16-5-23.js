/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-23.js
 * @description Array.prototype.every - number primitive can be used as thisArg
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return this.valueOf() === 101;
        }

        return [11].every(callbackfn, 101) && accessed;
    }
runTestCase(testcase);
