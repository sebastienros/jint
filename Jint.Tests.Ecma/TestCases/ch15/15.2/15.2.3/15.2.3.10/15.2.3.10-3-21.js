/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-21.js
 * @description Object.preventExtensions - named properties cannot be added into an Arguments object
 */


function testcase() {
        var argObj;
        (function () {
            argObj = arguments;
        }());
        var preCheck = Object.isExtensible(argObj);
        Object.preventExtensions(argObj);

        argObj.exName = 2;
        return preCheck && !argObj.hasOwnProperty("exName");
    }
runTestCase(testcase);
