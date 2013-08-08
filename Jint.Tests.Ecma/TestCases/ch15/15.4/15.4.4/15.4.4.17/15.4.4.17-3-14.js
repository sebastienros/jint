/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-3-14.js
 * @description Array.prototype.some - 'length' is a string containing +/-Infinity
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return val > 10;
        }

        var objOne = { 0: 11, length: "Infinity" };
        var objTwo = { 0: 11, length: "+Infinity" };
        var objThree = { 0: 11, length: "-Infinity" };

        return !Array.prototype.some.call(objOne, callbackfn) &&
            !Array.prototype.some.call(objTwo, callbackfn) &&
            !Array.prototype.some.call(objThree, callbackfn) && !accessed;
    }
runTestCase(testcase);
