/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.12/15.2.3.12-2-a-11.js
 * @description Object.isFrozen - 'O' is the Arguments object
 */


function testcase() {

        var arg;

        (function fun() {
            arg = arguments;
        }(1, 2, 3));

        Object.preventExtensions(arg);
        return !Object.isFrozen(arg);
    }
runTestCase(testcase);
