/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-1-1.js
 * @description Array.prototype.map - applied to undefined
 */


function testcase() {
        try {
            Array.prototype.map.call(undefined); // TypeError is thrown if value is undefined
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
