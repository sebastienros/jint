/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-23.js
 * @description Object.isExtensible returns false if 'O' is not extensible
 */


function testcase() {

        var obj = {};
        Object.preventExtensions(obj);
        return !Object.isExtensible(obj);

    }
runTestCase(testcase);
