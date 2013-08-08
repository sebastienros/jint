/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-31.js
 * @description Array.prototype.reduce - Date object can be used as accumulator
 */


function testcase() {

        var objDate = new Date();

        var accessed = false;
        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return prevVal === objDate;
        }

        var obj = { 0: 11, length: 1 };

        return Array.prototype.reduce.call(obj, callbackfn, objDate) === true && accessed;
    }
runTestCase(testcase);
