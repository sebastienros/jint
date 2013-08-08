/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-2-2.js
 * @description Array.prototype.every - 'length' is own data property on an Array
 */


function testcase() {
        function callbackfn1(val, idx, obj) {
            return val > 10;
        }

        function callbackfn2(val, idx, obj) {
            return val > 11;
        }

        try {
            Array.prototype[2] = 9;

            return [12, 11].every(callbackfn1) &&
                ![12, 11].every(callbackfn2);
        } finally {
            delete Array.prototype[2];
        }
    }
runTestCase(testcase);
