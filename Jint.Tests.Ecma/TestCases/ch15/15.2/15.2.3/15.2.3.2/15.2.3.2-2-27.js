/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-27.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (Error object)
 */


function testcase() {
        var obj = new Error();

        return Object.getPrototypeOf(obj) === Error.prototype;
    }
runTestCase(testcase);
