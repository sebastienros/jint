/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-8.js
 * @description Array.prototype.every - element to be retrieved is inherited data property on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 1) {
                return val !== 13;
            } else {
                return true;
            }
        }

        try {
            Array.prototype[1] = 13;
            return ![, , , ].every(callbackfn);
        } finally {
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);
