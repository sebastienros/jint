/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.13/15.2.3.13-2-29.js
 * @description Object.isExtensible returns true for the global object
 */


function testcase() {

        return Object.isExtensible(fnGlobalObject());

    }
runTestCase(testcase);
