/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-3.js
 * @description Array.prototype.reduce applied to boolean primitive
 */


function testcase() {

        function callbackfn(prevVal, curVal, idx, obj) {
            return  obj instanceof Boolean;
        }

        try {
            Boolean.prototype[0] = true;
            Boolean.prototype.length = 1;

            return Array.prototype.reduce.call(false, callbackfn, 1);

        } finally {
            delete Boolean.prototype[0];
            delete Boolean.prototype.length;
        }
    }
runTestCase(testcase);
