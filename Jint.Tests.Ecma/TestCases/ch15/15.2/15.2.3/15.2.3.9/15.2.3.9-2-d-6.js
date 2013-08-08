/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-2-d-6.js
 * @description Object.freeze - 'O' is a Date object
 */


function testcase() {
        var dateObj = new Date();

        Object.freeze(dateObj);

        return Object.isFrozen(dateObj);
    }
runTestCase(testcase);
