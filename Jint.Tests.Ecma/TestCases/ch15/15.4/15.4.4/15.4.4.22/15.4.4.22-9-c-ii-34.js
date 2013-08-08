/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-34.js
 * @description Array.prototype.reduceRight - Error Object can be used as accumulator
 */


function testcase() {

        var accessed = false;
        var objError = new RangeError();
        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return prevVal === objError;
        }

        var obj = { 0: 11, length: 1 };
        

        return Array.prototype.reduceRight.call(obj, callbackfn, objError) === true && accessed;
    }
runTestCase(testcase);
