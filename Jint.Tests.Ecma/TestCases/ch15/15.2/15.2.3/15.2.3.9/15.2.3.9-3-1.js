/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.9/15.2.3.9-3-1.js
 * @description Object.freeze - returned object is not extensible
 */


function testcase() {

        var obj = {};
        Object.freeze(obj);
        return !Object.isExtensible(obj);

    }
runTestCase(testcase);
