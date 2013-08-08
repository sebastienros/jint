/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-26.js
 * @description Array.prototype.reduce - Array object can be used as accumulator
 */


function testcase() {

        var objArray = new Array(10);

        var accessed = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return prevVal === objArray;
        }

        var obj = { 0: 11, length: 1 };

        return Array.prototype.reduce.call(obj, callbackfn, objArray) === true && accessed;
    }
runTestCase(testcase);
