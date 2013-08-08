/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-24.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (Number object)
 */


function testcase() {
        var obj = new Number(-3);

        return Object.getPrototypeOf(obj) === Number.prototype;
    }
runTestCase(testcase);
