/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.10/15.10.4/15.10.4.1/15.10.4.1-4.js
 * @description RegExp - the SyntaxError is not thrown when flags is 'gim'
 */


function testcase() {
        try {
            var regExpObj = new RegExp('abc', 'gim');

            return true;
        } catch (e) {
            return false;
        }
    }
runTestCase(testcase);
