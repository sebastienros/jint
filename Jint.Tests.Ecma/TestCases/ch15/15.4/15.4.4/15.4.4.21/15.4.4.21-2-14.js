/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-2-14.js
 * @description Array.prototype.reduce applied to the Array-like object that 'length' property doesn't exist
 */


function testcase() {

        var accessed = false;

        function callbackfn(prevVal, curVal, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 11, 1: 12 };

        return Array.prototype.reduce.call(obj, callbackfn, 1) === 1 && !accessed;

    }
runTestCase(testcase);
