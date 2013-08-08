/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-c-6.js
 * @description Object.seal - 'O' is a Date object
 */


function testcase() {

        var dateObj = new Date();
        var preCheck = Object.isExtensible(dateObj);
        Object.seal(dateObj);

        return preCheck && Object.isSealed(dateObj);
    }
runTestCase(testcase);
