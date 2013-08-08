/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-29.js
 * @description Array.prototype.reduceRight - Number Object can be used as accumulator
 */


function testcase() {

        var accessed = false;
        var objNumber = new Number();
        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return prevVal === objNumber;
        }

        var obj = { 0: 11, length: 1 };

        return Array.prototype.reduceRight.call(obj, callbackfn, objNumber) === true && accessed;
    }
runTestCase(testcase);
