/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-20.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (Function Object)
 */


function testcase() {
        var obj = function (a, b) {
            return a + b;
        };

        return Object.getPrototypeOf(obj) === Function.prototype;
    }
runTestCase(testcase);
