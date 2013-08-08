/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-3-18.js
 * @description Array.prototype.forEach - value of 'length' is a string that can't convert to a number
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
        }

        var obj = { 0: 9, length: "asdf!_" };

        Array.prototype.forEach.call(obj, callbackfn);

        return !accessed;
    }
runTestCase(testcase);
