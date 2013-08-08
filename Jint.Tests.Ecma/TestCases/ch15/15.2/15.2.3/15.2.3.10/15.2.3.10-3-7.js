/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-7.js
 * @description Object.preventExtensions - indexed properties cannot be added into a Number object
 */


function testcase() {
        var numObj = new Number(123);
        var preCheck = Object.isExtensible(numObj);
        Object.preventExtensions(numObj);

        numObj[0] = 12;
        return preCheck && !numObj.hasOwnProperty("0");
    }
runTestCase(testcase);
