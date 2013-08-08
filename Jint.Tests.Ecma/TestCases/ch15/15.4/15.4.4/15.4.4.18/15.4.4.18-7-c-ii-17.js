/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-c-ii-17.js
 * @description Array.prototype.forEach - 'this' of 'callbackfn' is a Number object when T is not an object (T is a number)
 */


function testcase() {

        var result = false;
        function callbackfn(val, idx, o) {
            result = (5 === this.valueOf());
        }

        var obj = { 0: 11, length: 2 };

        Array.prototype.forEach.call(obj, callbackfn, 5);
        return result;
    }
runTestCase(testcase);
