/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-15.js
 * @description Array.prototype.lastIndexOf applied to the Arguments object
 */


function testcase() {

        var obj = (function fun() {
            return arguments;
        }(1, 2, 3));

        return Array.prototype.lastIndexOf.call(obj, 2) === 1;
    }
runTestCase(testcase);
