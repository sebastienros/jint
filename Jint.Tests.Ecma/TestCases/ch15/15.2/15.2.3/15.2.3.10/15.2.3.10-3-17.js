/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-17.js
 * @description Object.preventExtensions - named properties cannot be added into a Number object
 */


function testcase() {
        var numObj = new Number(123);
        var preCheck = Object.isExtensible(numObj);
        Object.preventExtensions(numObj);

        numObj.exName = 2;
        return preCheck && !numObj.hasOwnProperty("exName");
    }
runTestCase(testcase);
