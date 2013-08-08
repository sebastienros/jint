/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-22.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (String object)
 */


function testcase() {
        var obj = new String("abc");

        return Object.getPrototypeOf(obj) === String.prototype;
    }
runTestCase(testcase);
