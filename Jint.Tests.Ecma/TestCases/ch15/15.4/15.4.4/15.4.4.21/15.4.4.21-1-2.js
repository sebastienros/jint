/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-2.js
 * @description Array.prototype.reduce applied to null
 */


function testcase() {
        try {
            Array.prototype.reduce.call(null);
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
