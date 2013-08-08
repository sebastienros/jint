/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-21.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (Array object)
 */


function testcase() {
        var arr = [1, 2, 3];

        return Object.getPrototypeOf(arr) === Array.prototype; 
    }
runTestCase(testcase);
