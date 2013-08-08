/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-2-34.js
 * @description Object.defineProperty - argument 'P' is applied to string 'AB
 * \cd' 
 */


function testcase() {
        var obj = {};
        Object.defineProperty(obj, "AB\n\\cd", {});

        return obj.hasOwnProperty("AB\n\\cd");

    }
runTestCase(testcase);
