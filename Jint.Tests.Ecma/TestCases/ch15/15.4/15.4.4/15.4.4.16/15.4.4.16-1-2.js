/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-1-2.js
 * @description Array.prototype.every applied to null throws a TypeError
 */


function testcase() {
        try {
            Array.prototype.every.call(null); // TypeError is thrown if value is null
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
