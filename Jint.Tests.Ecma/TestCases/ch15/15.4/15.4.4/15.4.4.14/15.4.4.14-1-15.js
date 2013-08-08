/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-1-15.js
 * @description Array.prototype.indexOf applied to Arguments object
 */


function testcase() {

        function fun() {
            return arguments;
        }
        var obj = fun(1, true, 3);

        return Array.prototype.indexOf.call(obj, true) === 1;
    }
runTestCase(testcase);
