/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-3-14.js
 * @description Array.prototype.every - 'length' is a string containing +/-Infinity
 */


function testcase() {

        var accessed = false;

        function callbackfn(val, idx, obj) {
            accessed = true;
            return val > 10;
        }

        var objOne = { 0: 9, length: "Infinity" };
        var objTwo = { 0: 9, length: "+Infinity" };
        var objThree = { 0: 9, length: "-Infinity" };

        return Array.prototype.every.call(objOne, callbackfn) &&
            Array.prototype.every.call(objTwo, callbackfn) &&
            Array.prototype.every.call(objThree, callbackfn) && !accessed;
    }
runTestCase(testcase);
