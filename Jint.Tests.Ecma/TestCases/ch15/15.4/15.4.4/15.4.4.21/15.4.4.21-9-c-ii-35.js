/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-35.js
 * @description Array.prototype.reduce - the Arguments object can be used as accumulator
 */


function testcase() {

        var accessed = false;
        var arg;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return prevVal === arg;
        }

        var obj = { 0: 11, length: 1 };

        (function fun() {
            arg = arguments;
        }(10, 11, 12, 13));

        return Array.prototype.reduce.call(obj, callbackfn, arg) === true && accessed;
    }
runTestCase(testcase);
