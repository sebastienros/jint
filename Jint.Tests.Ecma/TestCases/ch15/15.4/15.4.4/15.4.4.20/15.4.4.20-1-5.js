/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-5.js
 * @description Array.prototype.filter applied to number primitive
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof Number;
        }

        try {
            Number.prototype[0] = 1;
            Number.prototype.length = 1;

            var newArr = Array.prototype.filter.call(2.5, callbackfn);
            return newArr[0] === 1;
        } finally {
            delete Number.prototype[0];
            delete Number.prototype.length;
        }
    }
runTestCase(testcase);
