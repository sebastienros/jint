/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-22.js
 * @description Array.prototype.some - boolean primitive can be used as thisArg
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this.valueOf() === false;
        }

        return [11].some(callbackfn, false);
    }
runTestCase(testcase);
