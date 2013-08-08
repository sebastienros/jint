/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-4.js
 * @description Array.prototype.every - element to be retrieved is own data property that overrides an inherited data property on an Array
 */


function testcase() {
        var called = 0;
        function callbackfn(val, idx, obj) {
            called++;
            return val === 12;
        }

        try {
            Array.prototype[0] = 11;
            Array.prototype[1] = 11;

            return [12, 12].every(callbackfn) && called === 2;
        } finally {
            delete Array.prototype[0];
            delete Array.prototype[1];
        }
    }
runTestCase(testcase);
