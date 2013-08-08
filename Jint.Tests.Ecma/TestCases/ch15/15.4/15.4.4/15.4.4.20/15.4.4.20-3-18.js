/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-3-18.js
 * @description Array.prototype.filter - value of 'length' is a string that can't convert to a number
 */


function testcase() {

        var accessed = false;
        function callbackfn(val, idx, obj) {
            accessed = true;
            return true;
        }

        var obj = { 0: 9, length: "asdf!_" };

        var newArr = Array.prototype.filter.call(obj, callbackfn);

        return !accessed && newArr.length === 0;
    }
runTestCase(testcase);
