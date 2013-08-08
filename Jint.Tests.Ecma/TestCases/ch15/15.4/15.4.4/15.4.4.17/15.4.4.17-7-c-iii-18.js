/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-iii-18.js
 * @description Array.prototype.some - return value of callbackfn is a Boolean object
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return new Boolean();
        }

        return [11].some(callbackfn);
    }
runTestCase(testcase);
