/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-ii-17.js
 * @description Array.prototype.filter -'this' of 'callbackfn' is a Number object when T is not an object (T is a number)
 */


function testcase() {

        function callbackfn(val, idx, o) {
            return 5 === this.valueOf();
        }

        var obj = { 0: 11, length: 2 };
        var newArr = Array.prototype.filter.call(obj, callbackfn, 5);

        return newArr.length === 1 && newArr[0] === 11;
    }
runTestCase(testcase);
