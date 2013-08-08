/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.10/15.2.3.10-3-22.js
 * @description Object.preventExtensions - properties can still be deleted after extensions have been prevented
 */


function testcase() {
        var obj = { prop: 12 };
        var preCheck = Object.isExtensible(obj);
        Object.preventExtensions(obj);

        delete obj.prop;

        return preCheck && !obj.hasOwnProperty("prop");

    }
runTestCase(testcase);
