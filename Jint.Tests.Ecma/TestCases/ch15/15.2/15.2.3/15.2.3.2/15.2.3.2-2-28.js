/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-28.js
 * @description Object.getPrototypeOf returns the [[Prototype]] of its parameter (the Arguments object)
 */


function testcase() {
        function fun() {
            return arguments;
        }
        var obj = fun(1, true, 3);

        return Object.getPrototypeOf(obj) === Object.prototype;
    }
runTestCase(testcase);
