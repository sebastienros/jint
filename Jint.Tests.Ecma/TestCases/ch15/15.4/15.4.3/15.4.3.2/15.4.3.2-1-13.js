/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-1-13.js
 * @description Array.isArray applied to Arguments object
 */


function testcase() {

        var arg;

        (function fun() {
            arg = arguments;
        }(1, 2, 3));

        return !Array.isArray(arg);
    }
runTestCase(testcase);
