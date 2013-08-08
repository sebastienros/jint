/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-1-1.js
 * @description Array.prototype.forEach applied to undefined
 */


function testcase() {
        try {
            Array.prototype.forEach.call(undefined); // TypeError is thrown if value is undefined
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
