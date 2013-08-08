/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.1/15.1.2/15.1.2.3/15.1.2.3-2-1.js
 * @description pareseFloat - 'trimmedString' is the empty string when inputString does not contain any such characters
 */


function testcase() {
        return isNaN(parseFloat("")) && parseFloat("") !== parseFloat("");
    }
runTestCase(testcase);
