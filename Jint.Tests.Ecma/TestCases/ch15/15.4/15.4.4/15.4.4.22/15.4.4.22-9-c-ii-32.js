/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-32.js
 * @description Array.prototype.reduceRight - RegExp Object can be used as accumulator
 */


function testcase() {

        var accessed = false;
        var objRegExp = new RegExp();
        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
            return prevVal === objRegExp;
        }

        var obj = { 0: 11, length: 1 };

        return Array.prototype.reduceRight.call(obj, callbackfn, objRegExp) === true && accessed;
    }
runTestCase(testcase);
