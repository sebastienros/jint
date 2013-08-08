/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-1-1.js
 * @description Object.create throws TypeError if 'O' is undefined
 */


function testcase() {

        try {
            Object.create(undefined);
            return false;
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
