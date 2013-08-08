/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-14.js
 * @description Array.prototype.indexOf - 'length' is a string containing +/-Infinity
 */


function testcase() {

        var objOne = { 0: true, 1: true, length: "Infinity" };
        var objTwo = { 0: true, 1: true, length: "+Infinity" };
        var objThree = { 0: true, 1: true, length: "-Infinity" };

        return Array.prototype.indexOf.call(objOne, true) === -1 &&
            Array.prototype.indexOf.call(objTwo, true) === -1 &&
            Array.prototype.indexOf.call(objThree, true) === -1;
    }
runTestCase(testcase);
