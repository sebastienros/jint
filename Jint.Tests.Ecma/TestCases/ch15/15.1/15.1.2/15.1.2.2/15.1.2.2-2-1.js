/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.1/15.1.2/15.1.2.2/15.1.2.2-2-1.js
 * @description pareseInt - 'S' is the empty string when inputString does not contain any such characters
 */


function testcase() {
        return isNaN(parseInt("")) && parseInt("") !== parseInt("");
    }
runTestCase(testcase);
