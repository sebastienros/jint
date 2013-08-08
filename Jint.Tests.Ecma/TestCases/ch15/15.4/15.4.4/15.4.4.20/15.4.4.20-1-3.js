/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-1-3.js
 * @description Array.prototype.filter applied to boolean primitive
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return obj instanceof Boolean;
        }

        try {
            Boolean.prototype[0] = true;
            Boolean.prototype.length = 1;

            var newArr = Array.prototype.filter.call(false, callbackfn);
            return newArr[0] === true;

        } finally {
            delete Boolean.prototype[0];
            delete Boolean.prototype.length;
        }
    }
runTestCase(testcase);
