/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-17.js
 * @description Array.prototype.some - 'this' of 'callbackfn' is a Number object when T is not an object (T is a number primitive)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this.valueOf() === 5;
        }

        var obj = { 0: 11, length: 1 };

        return Array.prototype.some.call(obj, callbackfn, 5);
    }
runTestCase(testcase);
