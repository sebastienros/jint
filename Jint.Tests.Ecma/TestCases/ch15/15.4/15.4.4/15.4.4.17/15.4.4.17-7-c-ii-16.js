/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-ii-16.js
 * @description Array.prototype.some - 'this' of 'callback' is a Boolean object when 'T' is not an object ('T' is a boolean primitive)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this.valueOf() === false;
        }

        var obj = { 0: 11, length: 1 };

        return Array.prototype.some.call(obj, callbackfn, false);
    }
runTestCase(testcase);
