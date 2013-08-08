/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-a-12.js
 * @description Object.seal - 'P' is own property of a Date object that uses Object's [[GetOwnProperty]]
 */


function testcase() {
        var dateObj = new Date();

        dateObj.foo = 10;
        var preCheck = Object.isExtensible(dateObj);
        Object.seal(dateObj);

        delete dateObj.foo;
        return preCheck && dateObj.foo === 10;
    }
runTestCase(testcase);
