/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-16.js
 * @description Array.prototype.filter - 'this' of 'callbackfn' is a Boolean object when T is not an object (T is a boolean)
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return this.valueOf() === false;
        }

        var obj = { 0: 11, length: 2 };
        var newArr = Array.prototype.filter.call(obj, callbackfn, false);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
