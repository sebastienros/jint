/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-i-2.js
 * @description Array.prototype.every - element to be retrieved is own data property on an Array
 */


function testcase() {

        var called = 0;

        function callbackfn(val, idx, obj) {
            called++;
            return val === 11;
        }

        return [11].every(callbackfn) && 1 === called;
    }
runTestCase(testcase);
