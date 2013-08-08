/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-37.js
 * @description Object.defineProperty - argument 'P' is applied to string '123αβπcd' 
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, "123αβπcd", {});

        return obj.hasOwnProperty("123αβπcd");

    }
runTestCase(testcase);
