/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-1-3.js
 * @description Object.isExtensible throws TypeError if 'O' is a boolean
 */


function testcase() {

        try {
            Object.isExtensible(true);
        } catch (e) {
            return (e instanceof TypeError);
        }
    }
runTestCase(testcase);
