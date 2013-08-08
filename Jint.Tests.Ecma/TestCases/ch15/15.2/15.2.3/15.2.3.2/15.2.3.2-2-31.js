/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.2/15.2.3.2-2-31.js
 * @description Object.getPrototypeOf returns null
 */


function testcase() {

        return (Object.getPrototypeOf(Object.prototype) === null);
    }
runTestCase(testcase);
