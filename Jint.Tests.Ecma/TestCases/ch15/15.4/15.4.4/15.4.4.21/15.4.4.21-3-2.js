/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-3-2.js
 * @description Array.prototype.reduce - value of 'length' is a boolean (value is true)
 */


function testcase() {

        function callbackfn(prevVal, curVal, idx, obj) {
            return (curVal === 11 && idx === 0);
        }

        var obj = { 0: 11, 1: 9, length: true };

        return Array.prototype.reduce.call(obj, callbackfn, 1) === true;

    }
runTestCase(testcase);
