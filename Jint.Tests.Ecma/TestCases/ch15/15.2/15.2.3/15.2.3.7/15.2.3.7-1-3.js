/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-1-3.js
 * @description Object.defineProperties throws TypeError if 'O' is a boolean
 */


function testcase() {

        try {
            Object.defineProperties(true, {});
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
