/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-1-2.js
 * @description Object.getOwnPropertyNames throws TypeError if 'O' is undefined
 */


function testcase() {
        try {
            Object.getOwnPropertyNames(undefined);
            return false;
        } catch (e) {
            return e instanceof TypeError;
        }
    }
runTestCase(testcase);
