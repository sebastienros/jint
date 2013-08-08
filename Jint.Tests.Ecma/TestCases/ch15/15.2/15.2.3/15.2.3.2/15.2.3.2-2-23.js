/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-23.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (Boolean object)
 */


function testcase() {
        var obj = new Boolean(true);

        return Object.getPrototypeOf(obj) === Boolean.prototype;
    }
runTestCase(testcase);
