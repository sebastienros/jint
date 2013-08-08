/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-1-1.js
 * @description Array.prototype.reduce applied to undefined
 */


function testcase() {
        try {
            Array.prototype.reduce.call(undefined); 
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
