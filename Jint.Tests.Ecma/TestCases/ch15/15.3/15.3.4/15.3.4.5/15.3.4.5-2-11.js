/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-2-11.js
 * @description Function.prototype.bind throws TypeError if 'Target' is NULL
 */
function testcase() {
        try {
            Function.prototype.bind.call(null);
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
