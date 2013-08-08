/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-5.js
 * @description Array.prototype.reduce applied to number primitive
 */


function testcase() {
        function callbackfn(prevVal, curVal, idx, obj) {
            return obj instanceof Number;
        }

        try {
            Number.prototype[0] = 1;
            Number.prototype.length = 1;

            return Array.prototype.reduce.call(2.5, callbackfn, 1);
        } finally {
            delete Number.prototype[0];
            delete Number.prototype.length;
        }
    }
runTestCase(testcase);
