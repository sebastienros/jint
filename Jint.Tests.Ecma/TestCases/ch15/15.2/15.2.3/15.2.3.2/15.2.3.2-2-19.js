/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-19.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (Object object)
 */


function testcase() {
        var obj = {};

        return Object.getPrototypeOf(obj) === Object.prototype;
    }
runTestCase(testcase);
